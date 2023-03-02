﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MyAdmin.Admin;

public static class AdminEndpoints
{
    //[Authorize(Roles = "MyAdmin_Staff")]
    public static IResult AdminIndex(IOptions<AdminOptions> options)
    {
        return Results.Extensions.Render(options.Value.IndexTemplate);
    }

    //[Authorize(Roles = "MyAdmin_Staff")]
    public static async Task<IResult> ModelIndex<TContext>(
        int? page,
        [FromRoute] string modelName,
        [FromServices] TContext context,
        [FromServices] AdminServiceProvider admins)
        where TContext : DbContext
    {
        ModelAdmin? modelAdmin = admins.GetModelAdmin(modelName);

        if (modelAdmin == null)
        {
            return Results.NotFound();
        }

        Type modelType = modelAdmin.ModelType!;

        IQueryable<object>? dbset = context.Set(modelType) as IQueryable<object>;
        if (dbset == null)
        {
            return Results.NotFound();
        }

        PaginatedList<object> pages = await PaginatedList<object>.CreateAsync(dbset.AsNoTracking(), page ?? 1, 50);

        List<Dictionary<string, object?>> data = pages.ToDictionary();
        HashSet<string> properties = new();

        foreach (var prop in modelType.GetProperties())
        {
            properties.Add(prop.Name);
        }

        return Results.Extensions.Render(modelAdmin.Index_Template, null, new
        {
            ModelName = modelName,
            Data = data,
            Properties = properties,
        });
    }

    //[Authorize(Roles = "MyAdmin_Staff")]
    public static IResult ModelAdd_Get(
        [FromRoute] string modelName,
        [FromServices] IOptions<AdminOptions> options,
        [FromServices] AdminServiceProvider admins)
    {
        ModelAdmin? modelAdmin = admins.GetModelAdmin(modelName);

        if (modelAdmin == null)
        {
            return Results.NotFound();
        }

        Form formAdd = modelAdmin.GetForm();

        return Results.Extensions.Render(modelAdmin.FormAdd_Template, null, new
        {
            ModelName = modelName,
            Form = formAdd,
        });
    }

    //[Authorize(Roles = "MyAdmin_Staff")]
    public static async Task<IResult> ModelAdd_Post<TContext>([FromRoute] string modelName,
        HttpContext httpContext,
        [FromServices] TContext context,
        [FromServices] AdminServiceProvider admins,
        [FromServices] RouteHelper routeHelper)
        where TContext : DbContext
    {
        ModelAdmin? modelAdmin = admins.GetModelAdmin(modelName);

        if (modelAdmin == null)
        {
            return Results.NotFound();
        }

        Form form = modelAdmin.GetForm(httpContext.Request.Form);
        if (!await form.IsValid())
        {
            return Results.BadRequest();
        }

        form.Save(context);

        return Results.Redirect(routeHelper.Reverse("MyAdmin_ModelIndex", new { modelName }));
    }

    public static async Task<IResult> ModelChange_Get<TContext>(
        [FromRoute] string modelName,
        [FromRoute] string objIdentifier,
        [FromServices] AdminServiceProvider admins,
        [FromServices] TContext context)
        where TContext : DbContext
    {
        ModelAdmin? modelAdmin = admins.GetModelAdmin(modelName);
        if (modelAdmin == null)
        {
            return Results.NotFound();
        }

        Type modelType = modelAdmin.ModelType!;

        // Get the primary key property for the model type
        Type keyType = TypeHelper.FindKeyType(context, modelType);

        // Convert the ID string to the appropriate type
        object identifier = Convert.ChangeType(objIdentifier, keyType);

        // Call the Find method with the model type and ID to find the object
        object? instance = await context.FindAsync(modelType, identifier);

        if (instance == null)
        {
            return Results.NotFound();
        }

        Form form = modelAdmin.GetForm();
        form.SetWidgets(instance);

        return Results.Extensions.Render(modelAdmin.FormChange_Template, null, new
        {
            Form = form,
            instance,
            ModelName = modelName,
            identifier = objIdentifier,
        });
    }

    public static async Task<IResult> ModelChange_Post<TContext>(
        [FromRoute] string modelName,
        [FromRoute] string objIdentifier,
        HttpContext httpContext,
        [FromServices] TContext context,
        [FromServices] AdminServiceProvider admins,
        [FromServices] RouteHelper routeHelper)
        where TContext : DbContext
    {
        ModelAdmin? modelAdmin = admins.GetModelAdmin(modelName);

        if (modelAdmin == null)
        {
            return Results.NotFound();
        }

        // Assign and validate fields
        Form form = modelAdmin.GetForm(httpContext.Request.Form);

        if (!await form.IsValid())
        {
            return Results.BadRequest();
        }

        Type modelType = modelAdmin.ModelType!;
        Type keyType = TypeHelper.FindKeyType(context, modelType);

        object? identifier = Convert.ChangeType(objIdentifier, keyType);

        object? instance = await context.FindAsync(modelType, identifier);

        if (instance == null)
        {
            return Results.NotFound();
        }

        form.Save(context, instance);

        return Results.Redirect(routeHelper.Reverse("MyAdmin_ModelIndex", new { modelName }));
    }

    public static IResult ModelFetchData<TContext>(
        [FromRoute] string modelName,
        TContext dbContext,
        [FromServices] AdminServiceProvider admins)
        where TContext : DbContext
    {
        ModelAdmin? modelAdmin = admins.GetModelAdmin(modelName);
        if (modelAdmin == null)
        {
            return Results.NotFound();
        }

        Type modelType = modelAdmin.ModelType!;
        IQueryable? set = dbContext.Set(modelType);
        if (set == null) 
        {
            return Results.NotFound();
        }
        
        return Results.Ok(set);
    }
}
