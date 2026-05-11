# Estágio de Build
FROM node:20-alpine AS build-stage
WORKDIR /app

# Copiar arquivos de dependências primeiro para cache do Docker
COPY package.json package-lock.json ./
RUN npm ci

# Copiar todo o código-fonte do frontend
COPY . .

# Compilar a aplicação para produção (gera pasta dist/)
RUN npm run build

# Estágio de Produção (Servidor Nginx)
FROM nginx:alpine AS production-stage

# Copiar arquivo de configuração personalizada do Nginx
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copiar arquivos estáticos compilados do estágio anterior para o Nginx
COPY --from=build-stage /app/dist /usr/share/nginx/html

EXPOSE 80

# Iniciar o Nginx em primeiro plano para manter o container vivo
CMD ["nginx", "-g", "daemon off;"]
