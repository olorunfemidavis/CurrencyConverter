﻿# Currency Converter API

The **Currency Converter API** is a .NET 8.0 RESTful service for retrieving exchange rates and performing currency conversions. It integrates with the Frankfurter API for real-time and historical rates, supports Redis caching, and uses JWT authentication with role-based authorization. The solution follows Clean Architecture with CQRS, MediatR for query handling, and a factory pattern for currency providers.

## Table of Contents

1. [Features](#features)
2. [Architecture](#architecture)
3. [Prerequisites](#prerequisites)
4. [Setup Instructions](#setup-instructions)
5. [Assumptions Made](#assumptions-made)
6. [Running the Application](#running-the-application)
7. [API Endpoints](#api-endpoints)
8. [Authentication](#authentication)
9. [Testing](#testing)
10. [Project Structure](#project-structure)
11. [Possible Future Enhancements](#possible-future-enhancements)
12. [Dependencies](#dependencies)
13. [Contributing](#contributing)
14. [License](#license)

## Features

- **Latest Exchange Rates:** Fetch current rates for a base currency.
- **Currency Conversion:** Convert amounts between currencies.
- **Historical Rates:** Retrieve rates for a date range with pagination (Admin only).
- **Caching:** Redis caching (1 hour for latest/conversion, 24 hours for historical).
- **Authentication:** JWT-based with User and Admin roles.
- **Authorization:** Role-based access.
- **Provider Abstraction:** Factory pattern for currency providers (Frankfurter supported).
- **Excluded Currencies:** TRY, PLN, THB, MXN blocked.
- **Testing:** Unit and integration tests (90%+ coverage).
- **Logging:** Structured logging.
- **Resilience:** HTTP client resilience (retries, circuit breaker).

## Architecture

- **API Layer:** ASP.NET Core controllers (`RatesController`, `AuthController`).
- **Application Layer:** MediatR queries (`GetLatestRatesQuery`, `ConvertCurrencyQuery`, `GetHistoricalRatesQuery`).
- **Domain Layer:** Interfaces (`ICurrencyProvider`, `ICacheService`).
- **Infrastructure Layer:** Frankfurter API and Redis implementations.
- **Testing:** Unit (`CurrencyConverter.UnitTests`) and integration (`CurrencyConverter.IntegrationTests`) projects.

## Prerequisites

- .NET SDK 8.0+
- Docker (for Redis, optional for integration tests)
- IDE: Visual Studio 2022, JetBrains Rider, or VS Code
- Redis (local or Docker)
- Postman/Swagger (for API testing)
- Git

## Setup Instructions

1. **Clone the Repository:**
    ```bash
    git clone https://github.com/olorunfemidavis/CurrencyConverter.git
    cd currency-converter-api
    ```

2. **Restore Dependencies:**
    ```bash
    dotnet restore
    ```

3. **Set Up Redis:**
    - **Docker (recommended):**
        ```bash
        docker run -d -p 6379:6379 --name redis redis:latest
        ```
    - **Local Redis:**  
      Install from [redis.io](https://redis.io/download) or via package manager.  
      Start Redis on `localhost:6379`:
        ```bash
        redis-server
        ```

4. **Configure Environment:**  
   Create or update `CurrencyConverter.API/appsettings.json`:
    ```json
    {
      "Redis": {
        "ConnectionString": "localhost:6379"
      },
      "Frankfurter": {
        "BaseUrl": "https://api.frankfurter.app"
      },
      "Jwt": {
        "Key": "your-secure-key-here-32-characters-long",
        "Issuer": "CurrencyConverterAPI",
        "Audience": "CurrencyConverterAPI"
      },
      "CurrencyProvider": {
        "ActiveProvider": "Frankfurter"
      },
      "Logging": {
        "LogLevel": {
          "Default": "Information",
          "Microsoft.AspNetCore": "Warning"
        }
      }
    }
    ```
   > Ensure the JWT key is at least 32 characters.

5. **Build the Solution:**
    ```bash
    dotnet build
    ```

6. **Verify Setup:**
    - Ensure Redis is running (`redis-cli ping` should return `PONG`).
    - Check `.NET SDK` version:
        ```bash
        dotnet --version
        ```

## Assumptions Made

- Frankfurter API is available and reliable.
- Only Frankfurter API is implemented (others can be added).
- Redis is required for caching (no in-memory fallback in production).
- Simple username/password for token generation (testing only).
- Excluded currencies are enforced.
- Minimal input validation.
- Integration tests use a mocked cache; production uses real Redis.
- API runs over HTTPS locally.
- Console logging is used.
- No rate limiting.

## Running the Application

1. **Start the API:**
    ```bash
    cd CurrencyConverter.API
    dotnet run
    ```
   The API runs at `https://localhost:5001`.

2. **Access Swagger UI:**  
   [https://localhost:5001/swagger](https://localhost:5001/swagger)

3. **Generate JWT Token:**  
   Send a POST request to `/api/v1/auth/token`:
    ```json
    {
      "username": "test",
      "password": "password",
      "role": "User"
    }
    ```
   Or use curl:
    ```bash
    curl -X POST https://localhost:5001/api/v1/auth/token -H "Content-Type: application/json" -d "{\"username\":\"test\",\"password\":\"password\",\"role\":\"User\"}"
    ```

4. **Test Endpoints:**  
   Use Swagger or Postman with the JWT token in the `Authorization` header (`Bearer <token>`).

   Example:
    ```bash
    curl -X GET https://localhost:5001/api/v1/rates/latest?baseCurrency=EUR -H "Authorization: Bearer <token>"
    ```

## API Endpoints

All endpoints are under `/api/v1` and require authentication.

### AuthController

- **POST** `/api/v1/auth/token`  
  Request:
    ```json
    { "username": "test", "password": "password", "role": "User" }
    ```
  Response:
    ```json
    { "Token": "<jwt-token>" }
    ```

### RatesController

- **GET** `/api/v1/rates/latest?baseCurrency={currency}`  
  Roles: User, Admin  
  Example: `/api/v1/rates/latest?baseCurrency=EUR`  
  Response:
    ```json
    { "baseCurrency": "EUR", "date": "2025-06-12", "rates": { "USD": 1.1 } }
    ```

- **GET** `/api/v1/rates/convert?fromCurrency={from}&toCurrency={to}&amount={amount}`  
  Roles: User, Admin  
  Example: `/api/v1/rates/convert?fromCurrency=EUR&toCurrency=USD&amount=100`  
  Response:
    ```json
    { "baseCurrency": "EUR", "date": "2025-06-12", "rates": { "USD": 110 } }
    ```

- **GET** `/api/v1/rates/historical?baseCurrency={currency}&startDate={start}&endDate={end}&page={page}&pageSize={size}`  
  Roles: Admin  
  Example: `/api/v1/rates/historical?baseCurrency=EUR&startDate=2025-01-01&endDate=2025-01-05&page=1&pageSize=10`  
  Response:
    ```json
    {
      "baseCurrency": "EUR",
      "startDate": "2025-01-01",
      "endDate": "2025-01-05",
      "page": 1,
      "pageSize": 10,
      "totalRecords": 5,
      "rates": [...]
    }
    ```

**Notes:**
- Excluded currencies: TRY, PLN, THB, MXN (returns 400 Bad Request).
- Date format: `YYYY-MM-DD`.
- Amount must be positive.

## Authentication

- **JWT:** Tokens from `/api/v1/auth/token`, valid for 1 hour.
- **Roles:** User (latest/convert), Admin (all endpoints).
- **Config:** Set `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` in `appsettings.json`.

## Testing

### Unit Tests

- **Project:** `CurrencyConverter.UnitTests`
- **Framework:** xUnit
- **Packages:** Moq, FluentAssertions, coverlet.collector
- **Coverage:** Query handlers and provider.

    ```bash
    dotnet test CurrencyConverter.UnitTests/CurrencyConverter.UnitTests.csproj
    dotnet test CurrencyConverter.UnitTests/CurrencyConverter.UnitTests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
    ```

### Integration Tests

- **Project:** `CurrencyConverter.IntegrationTests`
- **Framework:** xUnit
- **Packages:** Microsoft.AspNetCore.Mvc.Testing, WireMock.Net, FluentAssertions
- **Coverage:** `RatesController` endpoints, JWT, mocked Frankfurter API, mocked Redis.

    ```bash
    dotnet test CurrencyConverter.IntegrationTests/CurrencyConverter.IntegrationTests.csproj
    dotnet test CurrencyConverter.IntegrationTests/CurrencyConverter.IntegrationTests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
    ```

> Integration tests use `MockCacheService` to avoid Redis dependency. For real Redis, install `Testcontainers.Redis` and ensure Docker is running.


### Code Coverage Report

The code coverage report for the Currency Converter API is generated using `dotnet-coverage` and `report-generator`, covering both integration and unit tests.

![Code Coverage Report](Code%20Coverage%20Report.png)


## Project Structure

```
CurrencyConverter/
├── CurrencyConverter.API/           # ASP.NET Core Web API
│   ├── Controllers/                # AuthController, RatesController
│   ├── Program.cs                  # Startup
│   ├── appsettings.json            # Config
├── CurrencyConverter.Application/  # CQRS queries
│   ├── Queries/                   # MediatR handlers
│   ├── DTOs/                      # DTOs
├── CurrencyConverter.Domain/       # Interfaces
│   ├── Interfaces/
├── CurrencyConverter.Infrastructure/ # Implementations
│   ├── Providers/                 # FrankfurterProvider
├── CurrencyConverter.UnitTests/    # Unit tests
│   ├── Queries/                   # Handler tests
│   ├── Providers/                 # Provider tests
├── CurrencyConverter.IntegrationTests/ # Integration tests
│   ├── Controllers/               # RatesController tests
├── CurrencyConverter.sln           # Solution file
```

## Possible Future Enhancements

- Add more currency providers via `ICurrencyProviderFactory`.
- Implement API rate limiting.
- Add in-memory cache fallback.
- Use a production-grade identity provider.
- Add stricter input validation.
- Integrate CI/CD pipelines.
- Add monitoring/telemetry.
- Store historical rates in a database.
- Support API versioning.
- Add WebSocket support for real-time updates.

## Dependencies

- .NET 8.0
- ASP.NET Core
- MediatR
- StackExchange.Redis
- Microsoft.Extensions.Http.Resilience
- WireMock.Net
- xUnit, Moq, FluentAssertions
- Microsoft.AspNetCore.Mvc.Testing
- System.IdentityModel.Tokens.Jwt

See `.csproj` files for versions.

## Contributing

1. Fork the repository.
2. Create a feature branch:
    ```bash
    git checkout -b feature/your-feature
    ```
3. Commit changes:
    ```bash
    git commit -m "Add your feature"
    ```
4. Push to the branch:
    ```bash
    git push origin feature/your-feature
    ```
5. Open a pull request.

**Guidelines:**
- Follow .NET coding standards.
- Add unit/integration tests for new features.
- Update documentation as needed.

## License
