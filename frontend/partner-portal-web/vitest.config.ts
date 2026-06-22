import { defineConfig, mergeConfig } from "vitest/config";
import viteConfig from "./vite.config";

// F15 — make the previously-implicit Vitest setup explicit. This changes NO behaviour: it merges the
// existing vite config (so the `@` → ./src alias and the React plugin are preserved, exactly as when
// Vitest auto-loaded vite.config.ts) and then states the defaults the suite already runs on:
//   • environment "node" — component tests use react-dom/server `renderToStaticMarkup`, never a DOM,
//     so jsdom/happy-dom are intentionally NOT used.
//   • include src/**/*.test.{ts,tsx} — every test lives under src and is named *.test.ts(x); there are
//     no *.spec files and no setupFiles.
export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: "node",
      include: ["src/**/*.test.{ts,tsx}"],
      globals: false,
    },
  }),
);
