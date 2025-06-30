using MessageService.Constants;
using MessageService.Data;
using MessageService.Hubs;
using MessageService.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHttpClient(ServiceConstants.ChatServiceHttpClientName, client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ChatService:BaseUrl"]
                                 ?? throw new InvalidOperationException("ChatService:BaseUrl not configured in app settings."));
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var redisConnectionString = configuration.GetConnectionString("RedisConnection") 
                                ?? throw new InvalidOperationException("RedisConnection string not found.");
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

builder.Services.AddScoped<IMessageRepository, MessageRepository>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IUserPresenceService, UserPresenceService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Run();