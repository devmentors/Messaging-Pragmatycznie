using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketFlow.CourseUtils;
using TicketFlow.Services.Inquiries.Core;
using TicketFlow.Services.Inquiries.Core.Commands.SubmitInquiry;
using TicketFlow.Services.Inquiries.Core.Commands.SubmitInquirySynchronously;
using TicketFlow.Services.Inquiries.Core.Data;
using TicketFlow.Services.Inquiries.Core.Queries;
using TicketFlow.Shared.AnomalyGeneration.HttpApi;
using TicketFlow.Shared.AspNetCore;
using TicketFlow.Shared.Commands;
using TicketFlow.Shared.Queries;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddCore(builder.Configuration)
    .AddApiForFrontendConfigured();

var app = builder.Build();

app.ExposeApiForFrontend();
app.UseAnomalyEndpoints();

app.MapGet("/", () => "Inquiries Service");

app.MapGet("/inquiries", async (
    [FromQuery] int page,
    [FromQuery] int limit,
    [FromServices] IQueryHandler<ListInquiries, InquiriesListDto> handler,
    CancellationToken cancellationToken) 
    => Results.Ok((object?)await handler.HandleAsync(new(page, limit), cancellationToken)));

app.MapPost("/inquiries/submit", async (
    [FromBody] SubmitInquiry command, 
    [FromServices] ICommandHandler<SubmitInquirySynchronously> synchronousHandler,
    [FromServices] ICommandHandler<SubmitInquiry> handler,
    CancellationToken cancellationToken) =>
{
    if (FeatureFlags.UseSynchronousIntegration)
    {
        var synchronousCommand = new SubmitInquirySynchronously(command.Name, command.Email, command.Title, command.Description, command.Category);
        await synchronousHandler.HandleAsync(synchronousCommand);
        return Results.Ok();
    }
    else
    {
        await handler.HandleAsync(command, cancellationToken);
        return Results.Ok();
    }
});

app.MapPost("/inquiries/submit-sync", async ([FromBody] SubmitInquirySynchronously command, [FromServices] ICommandHandler<SubmitInquirySynchronously> handler,
    CancellationToken cancellationToken) =>
{
    await handler.HandleAsync(command, cancellationToken);
    return Results.Ok();
});

app.MapGet("/inquiries/{inquiryId}", async ([FromRoute] Guid inquiryId, [FromServices] InquiriesDbContext dbContext, CancellationToken cancellationToken) =>
{
    var x = await dbContext.Inquiries.Where(x => x.Id == inquiryId).SingleOrDefaultAsync(cancellationToken);
    if (x == null)
    {
        return Results.NotFound();
    }
    
    return Results.Ok(new InquiriesListEntryDto(
        x.Id.ToString(),
        x.Name,
        x.Title,
        x.Email,
        x.Description,
        x.Category,
        x.Status,
        x.CreatedAt.ToString("O"),
        x.TicketId?.ToString()));
});

app.Run();

public partial class Program { }