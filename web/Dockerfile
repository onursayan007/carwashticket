# Bağlam repo kökü: docker build -f web/Dockerfile .
FROM node:24-alpine AS build
WORKDIR /src

COPY web/package.json web/package-lock.json ./
RUN npm ci

COPY web/ .

# Vite ortam değişkenlerini derleme anında gömüyor, çalışma anında değil.
# API aynı origin'de olduğu için taban adres boş: istekler /api/... göreli gider.
ARG VITE_API_BASE_URL=""
ARG VITE_MAPTILER_KEY=""
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_MAPTILER_KEY=$VITE_MAPTILER_KEY

RUN npm run build

FROM nginx:alpine

# nginx:alpine açılışta templates/ altını envsubst'ten geçirip conf.d'ye yazar.
# FILTER olmazsa $host, $remote_addr gibi nginx değişkenleri de silinir.
ENV API_UPSTREAM=api:8080
ENV NGINX_ENVSUBST_FILTER=API_UPSTREAM

COPY web/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/dist /usr/share/nginx/html

EXPOSE 80
