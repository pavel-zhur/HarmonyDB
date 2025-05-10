FROM nginx:alpine

# Install tools for certificate generation
RUN apk add --no-cache openssl

# Copy configuration
COPY cosmos-proxy/nginx.conf /etc/nginx/nginx.conf
COPY cosmos-proxy/generate-cert.sh /generate-cert.sh

# Make script executable
RUN chmod +x /generate-cert.sh

# Run the certificate generation script and start nginx
CMD ["/generate-cert.sh"] 