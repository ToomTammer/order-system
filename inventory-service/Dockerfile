FROM golang:1.25 AS build
WORKDIR /src

COPY go.mod go.sum ./
RUN go mod download

COPY . .
RUN CGO_ENABLED=0 go build -o /out/inventory-service ./cmd/inventory-service

FROM gcr.io/distroless/static-debian12 AS runtime
WORKDIR /app
COPY --from=build /out/inventory-service .
EXPOSE 8080
ENTRYPOINT ["/app/inventory-service"]
