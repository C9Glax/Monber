# Production build for the Nuxt 4 frontend, used only for docker-compose publishing (see
# MonberAPI.AppHost/AppHost.cs's IsPublishMode branch). Local dev still runs `npm run dev` via
# Aspire's AddNpmApp, not this image.
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
COPY --from=build /app/.output ./.output
ENV NUXT_HOST=0.0.0.0
ENV NUXT_PORT=3000
EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]
