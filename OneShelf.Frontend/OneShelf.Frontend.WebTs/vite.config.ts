import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { appSettings } from './build/appSettings';
import { globalStyles } from './build/globalStyles';
import { scopedJsx, scopedStyles } from './build/scopedStyles';
import { serviceWorker } from './build/serviceWorker';

const root = fileURLToPath(new URL('.', import.meta.url));

export default defineConfig({
    root,
    plugins: [
        react({ babel: { plugins: [scopedJsx(root)] } }),
        scopedStyles(),
        globalStyles(),
        appSettings(),
        serviceWorker(),
    ],
    build: {
        // The theme's files are in public/assets, so the application's own go elsewhere.
        assetsDir: '_app',
    },
    test: {
        include: ['src/**/*.test.ts', 'src/**/*.test.tsx', 'build/**/*.test.ts'],
    },
});
