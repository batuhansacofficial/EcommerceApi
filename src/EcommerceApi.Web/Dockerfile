FROM node:24-alpine AS build
WORKDIR /app

COPY src/EcommerceApi.Web/package*.json ./
COPY src/EcommerceApi.Web/pnpm-lock.yaml ./
RUN npm install -g pnpm@11.19.0 && pnpm install --frozen-lockfile

COPY src/EcommerceApi.Web/ .
RUN pnpm run build

FROM nginx:alpine AS final
COPY src/EcommerceApi.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
