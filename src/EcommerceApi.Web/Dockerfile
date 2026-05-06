FROM node:24-alpine AS build
WORKDIR /app

COPY src/EcommerceApi.Web/package*.json ./
RUN npm install

COPY src/EcommerceApi.Web/ .
RUN npm run build

FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
