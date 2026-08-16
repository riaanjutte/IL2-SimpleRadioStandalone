# IL2-SRS website

Astro + Starlight documentation site for SRS for IL-2 Community Edition.

## Local development

Run these commands from the `website` folder:

```powershell
npm install
npm run dev
```

The GitHub Pages build uses the repository base path `/IL2-SimpleRadioStandalone`. Production output is generated in `website/dist`:

```powershell
npm run build
npm run preview
```

## Content

Documentation pages are under `src/content/docs`. The homepage uses the Astro components in `src/components`, and project styling is in `src/styles/custom.css`.

## Deployment

`.github/workflows/deploy-pages.yml` builds this folder and deploys it with GitHub Actions. In the repository's **Settings → Pages**, set **Source** to **GitHub Actions** once before the first deployment.
