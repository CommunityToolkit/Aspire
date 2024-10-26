import {
  Application,
  Context,
  Router,
} from "https://deno.land/x/oak@v12.4.0/mod.ts";

const port = parseInt(Deno.env.get('PORT') || '8000');

const router = new Router();

interface Weather {
  Date: Date;
  TemperatureC: number;
}

router
  .get("/weather", async (ctx: Context) => {
    let forecast: Weather[] = [];
    for(var i = 0; i < 5; i++) {
      const weather: Weather = {
        Date: new Date(new Date().setDate(new Date().getDate() + i)),
        TemperatureC: Math.round(Math.random() * 36),
      };
      forecast.push(weather);
    }
    ctx.response.body = forecast;
  });

const app = new Application();

app.use(router.routes());
app.use(router.allowedMethods());

console.log(`Server listening on port ${port}`);
await app.listen({ port: port });