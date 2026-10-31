# sv

Everything you need to build a Svelte project, powered by [`sv`](https://github.com/sveltejs/cli).

## Creating a project

If you're seeing this, you've probably already done this step. Congrats!

```sh
# create a new project
npx sv create my-app
```

To recreate this project with the same configuration:

```sh
# recreate this project
npx sv@0.17.0 create --template minimal --types jsdoc --install npm tracklord-web
```

## Developing

Once you've created a project and installed dependencies with `npm install` (or `pnpm install` or `yarn`), start a development server:

```sh
npm run dev

# or start the server and open the app in a new browser tab
npm run dev -- --open
```

## Building

To create a production version of your app:

```sh
npm run build
```

You can preview the production build with `npm run preview`.

> To deploy your app, you may need to install an [adapter](https://svelte.dev/docs/kit/adapters) for your target environment.

## Vaani Voice Assistant

The authenticated floating **Talk to Vaani** button starts a WebRTC call using the TrackLord Vaani agent UUID configured in the frontend. The default UUID is the TrackLord Voice Assistant agent; set `VITE_VAANI_AGENT_ID` at build time only if using a different agent. The API key must be configured on the TrackLord API host as `VAANI_API_KEY`; it is never exposed to the browser. For local development, the API can also read `VAANI_API_KEY` from this app's ignored `.env` file.

Create a TrackLord agent in the Vaani dashboard and add the read-only TrackLord tools below under **Agent Config → Training → Tools & Actions**. Replace `https://YOUR_TRACKLORD_API` with the public HTTPS base URL for the deployed TrackLord API. For local development, use the restricted ngrok setup below rather than exposing the API directly. The Vaani tool request body must pass `voiceToken` (and `lotId` for lot-specific tools) through unchanged. In each tool's trigger instructions, tell the agent to use the exact `tracklord_voice_token` value from its system prompt as `voiceToken`.

The web call endpoint injects a 20-minute, user- and tenant-scoped read-only token into the agent prompt. Every tool checks the user's current TrackLord role permissions, and the token cannot authenticate to the regular TrackLord API. The tool endpoints only query data; they do not create or mutate records.

Add these endpoints as individual tools:

| Tool | Endpoint | Request body |
| --- | --- | --- |
| Dashboard overview | `POST /api/vaani-tools/dashboard-overview` | `{"voiceToken":"<voice-token-from-call>"}` |
| Production lots | `POST /api/vaani-tools/production-lots` | `{"voiceToken":"<voice-token-from-call>"}` |
| Production lot WIP | `POST /api/vaani-tools/production-lot-wip` | `{"voiceToken":"<voice-token-from-call>","lotId":"<lot-uuid>"}` |
| Production lot journey | `POST /api/vaani-tools/production-lot-journey` | `{"voiceToken":"<voice-token-from-call>","lotId":"<lot-uuid>"}` |
| Material traceability | `POST /api/vaani-tools/material-traceability` | `{"voiceToken":"<voice-token-from-call>","lotId":"<lot-uuid>"}` |
| Manufacturing steps | `POST /api/vaani-tools/manufacturing-steps` | `{"voiceToken":"<voice-token-from-call>"}` |
| Material lots | `POST /api/vaani-tools/material-lots` | `{"voiceToken":"<voice-token-from-call>"}` |
| Clients | `POST /api/vaani-tools/clients` | `{"voiceToken":"<voice-token-from-call>"}` |

Example request for the Dashboard overview tool:

```bash
curl -X POST 'https://YOUR_TRACKLORD_API/api/vaani-tools/dashboard-overview' \
  -H 'Content-Type: application/json' \
  -d '{"voiceToken":"<voice-token-from-call>"}'
```

Vaani's documented Agent Training API covers knowledge and guardrails, while external tool registration is configured in the dashboard; the TrackLord endpoints above are ready for that registration.

### Signing key configuration

The API requires two distinct signing secrets, each at least 32 bytes, and fails at startup if either is missing, too short, or reused. Keep both out of source control and browser code.

- For local development, .NET User Secrets is enabled for `TrackLord.Api`. Set `JwtSettings:SecretKey` and `VaaniVoiceToken:SecretKey` to different random values using `dotnet user-secrets set "<setting>" "<random-secret-at-least-32-bytes>" --project TrackLord.Api`.
- For Linux deployment, configure `JwtSettings__SecretKey` and `VaaniVoiceToken__SecretKey` as server-side environment variables (or use your deployment platform's secret manager). Use the same values across API instances so issued tokens validate consistently.

The Vaani API key must also be configured server-side as `VAANI_API_KEY`. Ensure the Linux reverse proxy forwards HTTPS POST requests and JSON bodies to the `/api/vaani-tools/...` routes. Once deployed, use `https://tracklord.shop` as the base URL for each Vaani tool.
