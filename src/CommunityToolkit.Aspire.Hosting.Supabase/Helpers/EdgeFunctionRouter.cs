using System.Text;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Helpers;

/// <summary>
/// Generates the Deno-based Edge Function Router for multi-function support.
/// </summary>
internal static class EdgeFunctionRouter
{
    /// <summary>
    /// Generates a TypeScript router that handles requests to multiple Edge Functions.
    /// The router transforms each function's code at runtime to use a specific port,
    /// then spawns it as a separate Deno process and proxies requests to it.
    /// </summary>
    /// <param name="path">Path to write the main.ts file</param>
    /// <param name="functionNames">List of available function names</param>
    public static void GenerateRouter(string path, List<string> functionNames)
    {
        var functionsJson = JsonSerializer.Serialize(functionNames);

        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated Edge Function Router/Proxy");
        sb.AppendLine("// DO NOT EDIT - This file is regenerated on each Aspire start");
        sb.AppendLine("// This router spawns each function as a separate Deno process and proxies requests to it.");
        sb.AppendLine();
        sb.AppendLine("const FUNCTIONS_DIR = \"/home/deno/functions\";");
        sb.AppendLine("const BASE_PORT = 9100; // Function worker ports start here");
        sb.AppendLine("const PROXY_PORT = parseInt(Deno.env.get(\"EDGE_RUNTIME_PORT\") || \"9000\");");
        sb.AppendLine();
        sb.AppendLine("const corsHeaders = {");
        sb.AppendLine("  'Access-Control-Allow-Origin': '*',");
        sb.AppendLine("  'Access-Control-Allow-Headers': 'authorization, x-client-info, apikey, content-type',");
        sb.AppendLine("  'Access-Control-Allow-Methods': 'GET, POST, PUT, DELETE, OPTIONS',");
        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine($"const availableFunctions: string[] = {functionsJson};");
        sb.AppendLine();
        sb.AppendLine("// Track running function workers");
        sb.AppendLine("const workers: Map<string, { process: Deno.ChildProcess; port: number }> = new Map();");
        sb.AppendLine("let nextPort = BASE_PORT;");
        sb.AppendLine();

        // startFunctionWorker function
        sb.AppendLine("async function startFunctionWorker(functionName: string): Promise<number> {");
        sb.AppendLine("  const existing = workers.get(functionName);");
        sb.AppendLine("  if (existing) {");
        sb.AppendLine("    return existing.port;");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  const port = nextPort++;");
        sb.AppendLine("  const functionPath = FUNCTIONS_DIR + \"/\" + functionName + \"/index.ts\";");
        sb.AppendLine();
        sb.AppendLine("  console.log(\"[Router] Starting worker for '\" + functionName + \"' on port \" + port);");
        sb.AppendLine();
        sb.AppendLine("  // Read the function file and transform it to use our port");
        sb.AppendLine("  const functionCode = await Deno.readTextFile(functionPath);");
        sb.AppendLine();
        sb.AppendLine("  // Transform the code: replace serve(...) with Deno.serve({ port }, ...)");
        sb.AppendLine("  // This handles the pattern: serve(async (req) => { ... })");
        sb.AppendLine("  let transformedCode = functionCode;");
        sb.AppendLine();
        sb.AppendLine("  // Replace the serve import with Deno.serve usage");
        sb.AppendLine("  // Remove the serve import line");
        sb.AppendLine("  transformedCode = transformedCode.replace(");
        sb.AppendLine("    /import\\s*\\{\\s*serve\\s*\\}\\s*from\\s*[\"']https:\\/\\/deno\\.land\\/std[^\"']*\\/http\\/server\\.ts[\"'];?/g,");
        sb.AppendLine("    '// serve import removed - using Deno.serve'");
        sb.AppendLine("  );");
        sb.AppendLine();
        sb.AppendLine("  // Replace serve( with Deno.serve({ port: PORT },");
        sb.AppendLine("  transformedCode = transformedCode.replace(");
        sb.AppendLine("    /\\bserve\\s*\\(/g,");
        sb.AppendLine("    'Deno.serve({ port: ' + port + ' }, '");
        sb.AppendLine("  );");
        sb.AppendLine();
        sb.AppendLine("  console.log('[Router] Transformed code for ' + functionName);");
        sb.AppendLine();
        sb.AppendLine("  const command = new Deno.Command(\"deno\", {");
        sb.AppendLine("    args: [");
        sb.AppendLine("      \"run\",");
        sb.AppendLine("      \"--allow-all\",");
        sb.AppendLine("      \"-\",  // Read from stdin");
        sb.AppendLine("    ],");
        sb.AppendLine("    stdin: \"piped\",");
        sb.AppendLine("    stdout: \"inherit\",");
        sb.AppendLine("    stderr: \"inherit\",");
        sb.AppendLine("    env: Deno.env.toObject(),");
        sb.AppendLine("  });");
        sb.AppendLine();
        sb.AppendLine("  const process = command.spawn();");
        sb.AppendLine();
        sb.AppendLine("  // Write the transformed code to stdin");
        sb.AppendLine("  const writer = process.stdin.getWriter();");
        sb.AppendLine("  await writer.write(new TextEncoder().encode(transformedCode));");
        sb.AppendLine("  await writer.close();");
        sb.AppendLine();
        sb.AppendLine("  workers.set(functionName, { process, port });");
        sb.AppendLine();
        sb.AppendLine("  // Wait for the worker to start");
        sb.AppendLine("  await new Promise((resolve) => setTimeout(resolve, 2000));");
        sb.AppendLine();
        sb.AppendLine("  return port;");
        sb.AppendLine("}");
        sb.AppendLine();

        // proxyRequest function
        sb.AppendLine("async function proxyRequest(req: Request, functionName: string, port: number): Promise<Response> {");
        sb.AppendLine("  const url = new URL(req.url);");
        sb.AppendLine("  const targetUrl = \"http://localhost:\" + port + url.pathname + url.search;");
        sb.AppendLine();
        sb.AppendLine("  console.log(\"[Router] Proxying to \" + targetUrl);");
        sb.AppendLine();
        sb.AppendLine("  try {");
        sb.AppendLine("    const headers = new Headers(req.headers);");
        sb.AppendLine();
        sb.AppendLine("    const proxyReq = new Request(targetUrl, {");
        sb.AppendLine("      method: req.method,");
        sb.AppendLine("      headers: headers,");
        sb.AppendLine("      body: req.body,");
        sb.AppendLine("      redirect: 'manual',");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    const response = await fetch(proxyReq);");
        sb.AppendLine();
        sb.AppendLine("    // Add CORS headers to response");
        sb.AppendLine("    const responseHeaders = new Headers(response.headers);");
        sb.AppendLine("    Object.entries(corsHeaders).forEach(([k, v]) => responseHeaders.set(k, v));");
        sb.AppendLine();
        sb.AppendLine("    return new Response(response.body, {");
        sb.AppendLine("      status: response.status,");
        sb.AppendLine("      statusText: response.statusText,");
        sb.AppendLine("      headers: responseHeaders,");
        sb.AppendLine("    });");
        sb.AppendLine("  } catch (error) {");
        sb.AppendLine("    console.error(\"[Router] Proxy error:\", error);");
        sb.AppendLine("    return new Response(JSON.stringify({ error: 'Proxy error', details: error.message }), {");
        sb.AppendLine("      status: 502,");
        sb.AppendLine("      headers: { ...corsHeaders, 'Content-Type': 'application/json' },");
        sb.AppendLine("    });");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Main server
        sb.AppendLine("console.log(\"[Router] Starting Edge Function Router on port \" + PROXY_PORT);");
        sb.AppendLine("console.log(\"[Router] Available functions: \" + availableFunctions.join(', '));");
        sb.AppendLine();
        sb.AppendLine("Deno.serve({ port: PROXY_PORT }, async (req: Request) => {");
        sb.AppendLine("  const url = new URL(req.url);");
        sb.AppendLine("  const path = url.pathname;");
        sb.AppendLine();
        sb.AppendLine("  // Handle CORS preflight");
        sb.AppendLine("  if (req.method === 'OPTIONS') {");
        sb.AppendLine("    return new Response(null, { headers: corsHeaders });");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  // Health check");
        sb.AppendLine("  if (path === '/health' || path === '/') {");
        sb.AppendLine("    return new Response(JSON.stringify({");
        sb.AppendLine("      status: 'ok',");
        sb.AppendLine("      functions: availableFunctions,");
        sb.AppendLine("      workers: Array.from(workers.keys()),");
        sb.AppendLine("    }), {");
        sb.AppendLine("      headers: { ...corsHeaders, 'Content-Type': 'application/json' },");
        sb.AppendLine("    });");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  // Extract function name from path: /functions/v1/{name} or /{name}");
        sb.AppendLine("  const match = path.match(/^\\/(?:functions\\/v1\\/)?([^\\/]+)/);");
        sb.AppendLine("  const functionName = match?.[1];");
        sb.AppendLine();
        sb.AppendLine("  console.log(\"[Router] Request: \" + req.method + \" \" + path + \" -> function: \" + functionName);");
        sb.AppendLine();
        sb.AppendLine("  if (!functionName) {");
        sb.AppendLine("    return new Response(JSON.stringify({ error: 'No function specified', available: availableFunctions }), {");
        sb.AppendLine("      status: 400,");
        sb.AppendLine("      headers: { ...corsHeaders, 'Content-Type': 'application/json' },");
        sb.AppendLine("    });");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  if (!availableFunctions.includes(functionName)) {");
        sb.AppendLine("    return new Response(JSON.stringify({ error: 'Function not found: ' + functionName, available: availableFunctions }), {");
        sb.AppendLine("      status: 404,");
        sb.AppendLine("      headers: { ...corsHeaders, 'Content-Type': 'application/json' },");
        sb.AppendLine("    });");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  try {");
        sb.AppendLine("    const port = await startFunctionWorker(functionName);");
        sb.AppendLine("    return await proxyRequest(req, functionName, port);");
        sb.AppendLine("  } catch (error) {");
        sb.AppendLine("    console.error(\"[Router] Error handling request for '\" + functionName + \"':\", error);");
        sb.AppendLine("    return new Response(JSON.stringify({ error: error.message || 'Internal error' }), {");
        sb.AppendLine("      status: 500,");
        sb.AppendLine("      headers: { ...corsHeaders, 'Content-Type': 'application/json' },");
        sb.AppendLine("    });");
        sb.AppendLine("  }");
        sb.AppendLine("});");

        File.WriteAllText(path, sb.ToString());
    }
}
