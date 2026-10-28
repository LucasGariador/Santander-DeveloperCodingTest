# Santander Developer Coding Test - Hacker News API

A RESTful API built with ASP.NET Core that retrieves the details of the best n stories from the Hacker News API, ordered by their score in descending order.

## How to Run the Application

To run the application locally, you will need the .NET 8.0 SDK installed on your machine. First, clone this repository and open your terminal. Navigate to the API project directory by executing `cd src/HackerNews.Api`. Once there, restore the dependencies and build the project using the `dotnet build` command. 

Finally, start the server by running `dotnet run`. You can then open your browser and navigate to the Swagger UI at `http://localhost:<PORT>/swagger` (replacing `<PORT>` with the port provided in your terminal) to test the endpoints interactively. Alternatively, you can use a tool like curl to send a GET request directly to `http://localhost:<PORT>/api/stories/best?n=10`. To run the unit tests, simply execute `dotnet test` from the root of the solution.

## Assumptions Made

During the development of this solution, several design decisions were made to ensure performance and accuracy. Regarding the ordering by score, the Hacker News `/v0/beststories.json` endpoint returns up to 200 IDs ordered by a proprietary algorithm rather than strictly by score. To fulfill the requirement of returning the best stories determined by their score, the API fetches the details of all available best stories concurrently, caches them, and sorts the entire batch by score descending in memory before taking the top requested amount. 

For the comment count mapping, the Hacker News API item model contains a kids array with only top-level comments and a descendants integer. I assumed that the requested comment count refers to the total number of comments in the thread, so it is mapped directly from the descendants property. Additionally, some stories on Hacker News are text posts that do not have an external URL, so for these items, the URI property is mapped to an empty string to avoid null reference issues. 

Finally, to avoid overloading the Hacker News Firebase API and prevent socket exhaustion, concurrent requests to fetch individual item details are throttled using a SemaphoreSlim, while Polly is used to provide a standard retry resilience strategy for transient HTTP errors. The caching strategy also uses concurrency locks to ensure that if the cache expires under heavy load, only a single thread will reach out to the Hacker News API to repopulate it, preventing cache stampedes.

## Enhancements (Given more time)

If given more time, I would implement a proactive background caching mechanism using an IHostedService or Background Worker to poll the Hacker News API every few minutes. This would keep the cache constantly warm, ensuring that every incoming API request is served in a few milliseconds without the initial lazy-loading latency spike for the first user. 

For a production environment with multiple instances running behind a load balancer, I would replace the in-memory cache with an IDistributedCache solution like Redis or the .NET HybridCache to share the cached stories across all nodes. I would also implement the ASP.NET Core native RateLimiter middleware to protect the endpoint from malicious abuse or excessive requests from single IP addresses. Lastly, I would integrate OpenTelemetry to expose metrics such as cache hit ratios and external API latency, along with structured logging for better production observability.