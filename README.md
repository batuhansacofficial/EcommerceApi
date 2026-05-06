# EcommerceApi

Full-stack e-commerce sample project built with:

- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- JWT authentication
- React + TypeScript + Vite
- Docker Compose

## Run the full app

From the project root:

```powershell
docker compose up -d --build
```

Frontend:

```text
http://localhost:3000
```

API:

```text
http://localhost:5211
```

PostgreSQL:

```text
localhost:5432
```

## Stop the app

```powershell
docker compose down
```

## Backend development build

```powershell
dotnet build
```

## Frontend development build

```powershell
cd .\src\EcommerceApi.Web
npm.cmd run build
```

## Main API endpoints

### Products

```text
GET    /api/products
GET    /api/products/{id}
POST   /api/products
PUT    /api/products/{id}
DELETE /api/products/{id}
```

Product write endpoints require the Admin role.

### Auth

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me
```

### Cart

```text
GET    /api/cart
POST   /api/cart/items
PUT    /api/cart/items/{id}
DELETE /api/cart/items/{id}
```

Cart endpoints require authentication.

### Checkout

```text
POST /api/checkout
```

Checkout requires authentication.

### Orders

```text
GET /api/orders
GET /api/orders/{id}
```

Order endpoints require authentication.
