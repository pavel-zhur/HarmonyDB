import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { serviceWorker } from './build/serviceWorker.ts';
import { fingerings as fingeringsPieces } from './build/fingerings.ts';
import { engraverStyles } from './build/engraverStyles.ts';

const root = fileURLToPath(new URL('.', import.meta.url));

// The fingerings' model (the chords' fingering pictures): one file, kept outside the app's folder, split at build time
// into small pieces (build/fingerings.ts). SHELF_FINGERINGS names it (the image gets it from the build context
// "fingerings": docker/Dockerfile); not set, the old frontend's copy in this repository.
const fingerings = process.env.SHELF_FINGERINGS
    ?? fileURLToPath(new URL('../../OneShelf.Frontend/OneShelf.Frontend.WebTs/src/resources/fingerings.json', import.meta.url));

export default defineConfig({
    root,
    plugins: [react(), fingeringsPieces(fingerings), serviceWorker(), engraverStyles()],
    // the score's engraver is patched where it is bundled (build/engraverStyles.ts): in the dev server's pre-bundling too
    optimizeDeps: { rolldownOptions: { plugins: [engraverStyles()] } },
    build: {
        // every file a file of its own (no data: addresses: the page's content policy allows the app's own files only)
        assetsInlineLimit: 0,
        rollupOptions: {
            // The entries of the build: the app; the public page (architecture, section 15) is added here as
            // `public: 'public.html'` — it shares src/ but not the app's frame, worker or manifest.
            input: { app: fileURLToPath(new URL('index.html', import.meta.url)) },
        },
    },
    server: {
        // `npm run dev`: the stand's web server (docker/wave2, shelf-web) answers the server's addresses
        proxy: {
            '/api': 'http://localhost:14100',
            '/mcp': 'http://localhost:14100',
            '/oauth': 'http://localhost:14100',
            '/.well-known': 'http://localhost:14100',
        },
    },
});
