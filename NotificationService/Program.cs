using Microsoft.EntityFrameworkCore;
using NotificationService.BackgroundServices;
using NotificationService.Data;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

var messageServiceUrl = builder.Configuration["MessageService:Url"] ?? "http://localhost:5240";
var userServiceUrl = builder.Configuration["UserService:Url"] ?? "http://localhost:5267";

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseInMemoryDatabase("NotificationDb"));

builder.Services.AddHttpClient("MessageServiceApi", client =>
{
    client.BaseAddress = new Uri(messageServiceUrl);
});
builder.Services.AddHttpClient("UserServiceApi", client =>
{
    client.BaseAddress = new Uri(userServiceUrl);
});

builder.Services.AddScoped<IMessagePollingService, MessagePollingService>();
builder.Services.AddScoped<IEmailService, DummyEmailService>();

builder.Services.AddHostedService<NotificationBackgroundService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.Run();