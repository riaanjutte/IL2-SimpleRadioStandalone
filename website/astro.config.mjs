// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// https://astro.build/config
export default defineConfig({
	site: 'https://riaanjutte.github.io',
	base: '/IL2-SimpleRadioStandalone',
	integrations: [
		starlight({
			title: 'IL2-SRS Documentation',
			description:
				'Standalone voice communications for IL-2 Great Battles and IL-2 Korea.',
			favicon: '/favicon.svg',
			lastUpdated: true,
			customCss: ['./src/styles/custom.css'],
			components: {
				Header: './src/components/Header.astro',
			},
			social: [
				{
					icon: 'github',
					label: 'GitHub repository',
					href: 'https://github.com/riaanjutte/IL2-SimpleRadioStandalone',
				},
			],
			editLink: {
				baseUrl:
					'https://github.com/riaanjutte/IL2-SimpleRadioStandalone/edit/master/website/',
			},
			sidebar: [
				{ label: 'Overview', slug: 'index' },
				{ label: 'Downloads & releases', slug: 'releases' },
				{ label: 'Compatibility', slug: 'compatibility' },
				{ label: 'Screenshots', slug: 'screenshots' },
				{
					label: 'User guides',
					items: [
						{ label: 'Installation', slug: 'guides/installation' },
						{ label: 'Quick start', slug: 'guides/quick-start' },
						{ label: 'Controls and PTT', slug: 'guides/controls' },
						{ label: 'Overlays', slug: 'guides/overlays' },
					],
				},
				{
					label: 'Troubleshooting',
					items: [
						{ label: 'Telemetry and auto-connect', slug: 'troubleshooting/telemetry' },
						{ label: 'Audio', slug: 'troubleshooting/audio' },
					],
				},
				{
					label: 'Server administrators',
					items: [
						{ label: 'Server setup', slug: 'server-admin/server-setup' },
						{ label: 'Pilot Roster integration', slug: 'server-admin/pilot-roster' },
					],
				},
				{
					label: 'Contributing',
					items: [{ label: 'Translations', slug: 'contributing/translations' }],
				},
			],
		}),
	],
});
