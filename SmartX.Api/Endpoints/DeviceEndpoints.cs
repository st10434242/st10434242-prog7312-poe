using SmartX.Core;
using SmartX.Core.Contracts;

namespace SmartX.Api.Endpoints;

// Live device state for the command stream page, separate from /api/sensors which is about registration
public static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/devices").WithTags("Devices");

        group.MapGet("/", Find);
        group.MapGet("/{nodeId}", GetOne);

        return group;
    }

    // e.g. /api/devices?category=Environmental&alertState=Alert&search=Zone1
    private static IResult Find(
        DeviceTable devices,
        SensorCategory? category = null,
        DeviceAlertState? alertState = null,
        string? search = null) =>
        Results.Ok(devices.Find(category, alertState, search?.Trim()));

    private static IResult GetOne(string nodeId, DeviceTable devices)
    {
        DeviceDto? device = devices.Get(nodeId);
        return device is null ? Results.NotFound() : Results.Ok(device);
    }
}
