FROM alpine:latest

RUN apk add --no-cache openssl docker-cli bash

COPY renew.sh /usr/local/bin/renew.sh
RUN chmod +x /usr/local/bin/renew.sh

VOLUME ["/certs"]

CMD ["/usr/local/bin/renew.sh"]
