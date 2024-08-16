using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

// Core classes
public class Trade
{
    public string Id { get; set; }
    public string Symbol { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string TraderName { get; set; }
    public DateTime Timestamp { get; set; }
}

public class Portfolio
{
    public string Id { get; set; }
    public string ManagerName { get; set; }
    public List<Trade> Trades { get; set; }
}

public class Broker
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<string> SupportedSymbols { get; set; }
}

// SignalR Hub for real-time updates
public class TradingHub : Hub
{
    public async Task SendTradeUpdate(Trade trade)
    {
        await Clients.All.SendAsync("ReceiveTradeUpdate", trade);
    }
}

// API Controller
[ApiController]
[Route("api/[controller]")]
public class TradesController : ControllerBase
{
    private readonly IMongoCollection<Trade> _trades;
    private readonly IHubContext<TradingHub> _hubContext;

    public TradesController(IMongoClient mongoClient, IHubContext<TradingHub> hubContext)
    {
        var database = mongoClient.GetDatabase("TradingApp");
        _trades = database.GetCollection<Trade>("Trades");
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTrade([FromBody] Trade trade)
    {
        trade.Id = Guid.NewGuid().ToString();
        trade.Timestamp = DateTime.UtcNow;

        await _trades.InsertOneAsync(trade);
        await _hubContext.Clients.All.SendAsync("ReceiveTradeUpdate", trade);

        return CreatedAtAction(nameof(GetTrade), new { id = trade.Id }, trade);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Trade>> GetTrade(string id)
    {
        var trade = await _trades.Find(t => t.Id == id).FirstOrDefaultAsync();

        if (trade == null)
        {
            return NotFound();
        }

        return trade;
    }
}

// Startup.cs configuration (partial)
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMongoClient>(sp => 
            new MongoClient(Configuration.GetConnectionString("MongoDB")));
        services.AddSignalR();
        services.AddControllers();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHub<TradingHub>("/tradingHub");
        });
    }
}
