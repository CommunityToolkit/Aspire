import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addStreamlitApp — Streamlit app resource
const dashboard = await builder.addStreamlitApp("dashboard", "./streamlit-app", "app.py");
const _dashboardResource = await dashboard;

await builder.build().run();
