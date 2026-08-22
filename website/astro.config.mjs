// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// https://astro.build/config
export default defineConfig({
	site: 'https://srsforil2.com',
	base: '/',
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
				{ label: 'Support', slug: 'support' },
				{ label: 'Compatibility', slug: 'compatibility' },
				{ label: 'Screenshots', slug: 'screenshots' },
				{
					label: 'User guides',
					items: [
						{ label: 'Installation', slug: 'guides/installation' },
						{ label: 'Updates and recovery', slug: 'guides/update-recovery' },
						{ label: 'Quick start', slug: 'guides/quick-start' },
						{ label: 'Using both radios', slug: 'guides/two-radios' },
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
						{ label: 'Configuration reference', slug: 'server-admin/configuration-reference' },
						{ label: 'Radio behavior', slug: 'server-admin/radio-behavior' },
						{ label: 'Client administration', slug: 'server-admin/client-administration' },
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
