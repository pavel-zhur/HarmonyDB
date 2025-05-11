FROM nginx:alpine

# Copy nginx configuration
COPY api-proxy/nginx.conf /etc/nginx/conf.d/default.conf

# Expose ports for both frontend-api and collectives-api
EXPOSE 13011 13009 