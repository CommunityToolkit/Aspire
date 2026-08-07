import http from "node:http";

const server = http.createServer((request, response) => {
  response.writeHead(200, { "content-type": "application/json" });
  response.end(JSON.stringify({
    ok: true,
    path: request.url,
    greeting: process.env.GREETING ?? null
  }));
});

const port = Number(process.env.PORT ?? 80);
server.listen(port, "0.0.0.0", () => {
  console.log(`listening on ${port}`);
});
