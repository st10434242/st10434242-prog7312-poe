FROM node:22-alpine AS build
WORKDIR /src

# package-lock is copied too so npm ci installs the exact versions that were tested
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# nginx serves the built files and proxies the gateway, so the browser sees one origin
FROM nginx:alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html

EXPOSE 80
