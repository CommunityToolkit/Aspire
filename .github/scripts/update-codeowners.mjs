import fs from "node:fs";

const codeownersPath = process.env.CODEOWNERS_PATH ?? "CODEOWNERS";
const ownerLogin = process.env.CODEOWNER_LOGIN;
const exampleFoldersJson = process.env.EXAMPLE_FOLDERS_JSON;
const packageIdsJson = process.env.PACKAGE_IDS_JSON;

if (!ownerLogin || !exampleFoldersJson || !packageIdsJson) {
    throw new Error("CODEOWNER_LOGIN, EXAMPLE_FOLDERS_JSON, and PACKAGE_IDS_JSON are required.");
}

const exampleFolders = [...new Set(JSON.parse(exampleFoldersJson))];
const packageIds = [...new Set(JSON.parse(packageIdsJson))];
const owner = ownerLogin.startsWith("@") ? ownerLogin : `@${ownerLogin}`;
const packageIdPattern = /^CommunityToolkit\.Aspire(?:\.Hosting)?(?:\.[A-Za-z0-9]+)+$/;
const exampleFolderPattern = /^[a-z0-9-]+$/;

if (exampleFolders.length === 0) {
    throw new Error("At least one example folder is required.");
}

for (const exampleFolder of exampleFolders) {
    if (!exampleFolderPattern.test(exampleFolder)) {
        throw new Error(`Invalid example folder '${exampleFolder}'.`);
    }
}

for (const packageId of packageIds) {
    if (!packageIdPattern.test(packageId)) {
        throw new Error(`Invalid package ID '${packageId}'.`);
    }

    if (packageId.endsWith(".Tests")) {
        throw new Error(`Package ID '${packageId}' should not include the .Tests suffix.`);
    }
}

const originalContent = fs.readFileSync(codeownersPath, "utf8");
const content = originalContent.replace(/\r\n/g, "\n");
const ownershipLines = [
    ...exampleFolders.map((exampleFolder) => `/examples/${exampleFolder}/ ${owner}`),
    ...packageIds.flatMap((packageId) => [
        `/src/${packageId}/ ${owner}`,
        `/tests/${packageId}.Tests/ ${owner}`,
    ]),
];

for (const line of ownershipLines) {
    const prefix = line.split(" ")[0];
    const existingLine = content
        .split("\n")
        .find((entry) => entry.startsWith(`${prefix} `));

    if (existingLine && existingLine !== line) {
        throw new Error(`Conflicting CODEOWNERS entry already exists: '${existingLine}'.`);
    }
}

if (ownershipLines.every((line) => content.includes(line))) {
    process.exit(0);
}

const packageHeader = packageIds.length === 1
    ? `# ${packageIds[0]}`
    : `# ${packageIds.join(", ")}`;

const block = [
    packageHeader,
    "",
    ...ownershipLines,
    "",
].join("\n");

const updatedContent = `${content.trimEnd()}\n\n${block}\n`;
fs.writeFileSync(codeownersPath, updatedContent, "utf8");
