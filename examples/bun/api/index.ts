import { serve } from "bun";

serve({
    port: process.env.PORT || 3000,
    fetch() {
        return new Response("Hello, Bun!");
    },
});

console.log(
    `Server is running on http://localhost:${process.env.PORT || 3000}`
);
