import { defineConfig } from "vite-plus";

export default defineConfig({
    pack: {
        deps: {
            // tsdown <0.23 compatibility: resolve external dependency subpaths.
            // Remove to preserve subpath imports as written (the new default).
            // https://tsdown.dev/options/dependencies#deps-resolvedepsubpath
            resolveDepSubpath: true,
        },
        dts: {
            generator: "tsgo",
        },
        exports: true,
    },
});