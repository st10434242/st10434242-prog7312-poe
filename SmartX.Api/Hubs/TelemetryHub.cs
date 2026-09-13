using Microsoft.AspNetCore.SignalR;

namespace SmartX.Api.Hubs;

// Empty because traffic only flows outwards, the dashboard listens and never calls in
public sealed class TelemetryHub : Hub
{
}
