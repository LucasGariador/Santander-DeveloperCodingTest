# Santander Developer Coding Test - Hacker News API

A high-performance RESTful API built with ASP.NET Core (.NET 8) that retrieves the details of the best $n$ stories from the Hacker News API, strictly ordered by their score in descending order.

## How to Run the Application

### Option 1: Using the .NET CLI
To run the application locally, ensure you have the .NET 8.0 SDK installed. Clone this repository, open your terminal, and navigate to the API project directory by executing `cd src/HackerNews.Api`. Restore the dependencies and build the project using `dotnet build`, then start the server by running `dotnet run`. 

Once started, open your browser and navigate to the Swagger UI at `http://localhost:<PORT>/swagger` (replacing `<PORT>` with the port shown in your terminal) to test the endpoint interactively, or send a GET request directly to `http://localhost:<PORT>/api/stories/best?n=10`. To run the unit test suite, execute `dotnet test` from the root of the solution.

### Option 2: Using Docker
The repository includes a multi-stage `Dockerfile` for containerized execution. From the root directory of the solution, build the image by running `docker build -t hackernews-api .` and start the container with `docker run -p 8080:8080 hackernews-api`. The API and Swagger UI will be immediately available at `http://localhost:8080/swagger`.

## Assumptions & Architectural Decisions

During the development of this solution, several architectural decisions were made to guarantee both strict accuracy and ultra-low latency under heavy load.

Regarding the ordering by score, the Hacker News `/v0/beststories.json` endpoint returns up to 200 story IDs ranked by a proprietary aging algorithm rather than strictly by score. To accurately fulfill the requirement of returning the best $n$ stories determined by their score, the API fetches the details of all 200 best stories concurrently, caches them, and sorts the entire batch by score descending in memory before returning the top $n$ requested items.

To eliminate initial cold-start latency for users and prevent overloading the Hacker News API, a proactive background service (`CacheWarmupWorker`) warms up the cache upon application startup and refreshes it periodically in the background. Additionally, if a request arrives while the cache is cold, concurrency locks (`SemaphoreSlim`) prevent cache stampedes by ensuring only a single thread fetches data from Firebase while other concurrent requests wait for the populated cache.

At the network layer, outbound requests to Firebase are throttled using a `SemaphoreSlim` to avoid rate limiting and socket exhaustion. The underlying `SocketsHttpHandler` is explicitly configured to leverage HTTP/2 multiplexing and IPv4 fast-path resolution, reducing the cold-fetch time of 200 concurrent requests to ~2.5 seconds. Transient network failures are handled gracefully using Polly retry policies with exponential backoff.

For data mapping, the Hacker News API item model contains both a `kids` array (top-level comments only) and a `descendants` integer. This implementation assumes `commentCount` refers to the total number of comments in the thread and maps it from `descendants`. Text-only posts ("Ask HN") that do not contain an external URL have their `uri` mapped to an empty string.

## Enhancements (Given More Time)

For a multi-node production environment running behind a load balancer, I would replace `IMemoryCache` with a distributed caching layer such as Redis (`IDistributedCache` or .NET `HybridCache`) so that all instances share the pre-warmed story data without duplicating background polling to Hacker News.

I would also implement the native ASP.NET Core `RateLimiter` middleware to protect our public endpoint from abuse or DDoS scenarios per client IP, and integrate OpenTelemetry for distributed tracing, structured logging (Serilog), and metrics exposition (cache hit/miss ratios and upstream Firebase latency) alongside `/health` readiness probes.