import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();
const message = await builder.addParameter("message", { value: "Hello from Aspire" });
const terraform = await builder.addTerraformEnvironment("sample", "../module");
await terraform.withTfVar("message", message);
await terraform.withInit({ configure: async init => { await init.withExplicitStart(); } });
await terraform.withPlan({ configure: async plan => { await plan.withExplicitStart(); } });
await terraform.withApply({ configure: async apply => { await apply.withExplicitStart(); } });
await terraform.withDestroy();
await terraform.withPipelineOptions(async options => { await options.chainSteps.set(false); });
await builder.build().run();
