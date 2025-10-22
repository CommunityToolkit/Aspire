import { Application, Router, Context } from "@oak/oak";

const port = parseInt(Deno.env.get("PORT") || "8000");

const router = new Router();

interface Weather {
    Date: Date;
    TemperatureC: number;
}

router.get("/weather", (ctx: Context) => {
    const forecast: Weather[] = [];
    for (let i = 0; i < 5; i++) {
        const weather: Weather = {
            Date: new Date(new Date().setDate(new Date().getDate() + i)),
            TemperatureC: Math.round(Math.random() * 36),
        };
        forecast.push(weather);
    }
    ctx.response.body = forecast;
});

router.get("/health", (ctx: Context) => {
    ctx.response.body = { status: "ok" };
});

const app = new Application();

app.use(router.routes());
app.use(router.allowedMethods());

console.log(`Server listening on port ${port}`);
await app.listen({ port });
