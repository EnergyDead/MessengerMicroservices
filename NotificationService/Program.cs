using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.Configuration;
using NotificationService.BackgroundServices;
using NotificationService.Constants;
using NotificationService.Data;
using NotificationService.Services;

var builder = WebApplication.CreateBuilder(args);

var messageServiceUrl = builder.Configuration["MessageService:Url"] ?? throw new InvalidConfigurationException();
var userServiceUrl = builder.Configuration["UserService:Url"] ?? throw new InvalidConfigurationException();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHttpClient(ServiceConstants.MessageServiceHttpClientName, client => { client.BaseAddress = new Uri(messageServiceUrl); });
builder.Services.AddHttpClient(ServiceConstants.UserServiceHttpClientName, client => { client.BaseAddress = new Uri(userServiceUrl); });

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