# The page of the public links to the songs people add themselves (OneShelf.Frontend.StaticWeb): one static page that
# asks the Index for the chords at its own address.
# Build context: the repository root; the additional context "assets": this folder.
FROM nginx:1.27-alpine
COPY OneShelf.Frontend/OneShelf.Frontend.StaticWeb/wwwroot /usr/share/nginx/html
COPY --from=assets nginx.conf /etc/nginx/conf.d/default.conf
