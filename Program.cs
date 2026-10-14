using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Data;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options=>options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
var dataDirectory=Path.Combine(builder.Environment.ContentRootPath,"App_Data");Directory.CreateDirectory(dataDirectory);
builder.Services.AddDbContext<AppDbContext>(options=>options.UseSqlite(builder.Configuration.GetConnectionString("PropertyDb")??$"Data Source={Path.Combine(dataDirectory,"property.db")}"));
var app=builder.Build();
CultureInfo.DefaultThreadCurrentCulture=CultureInfo.GetCultureInfo("zh-TW");
CultureInfo.DefaultThreadCurrentUICulture=CultureInfo.GetCultureInfo("zh-TW");
if(!app.Environment.IsDevelopment())app.UseExceptionHandler("/Home/Error");
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".glb"] = "model/gltf-binary";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });app.UseRouting();
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}");
using(var scope=app.Services.CreateScope()) {
 var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
 db.Database.EnsureCreated();
 SeedData.EnsureSchema(db);
 if(app.Environment.IsDevelopment()&&builder.Configuration.GetValue("SeedDemoData",true))SeedData.Initialize(db);
}
app.Run();
public partial class Program;
