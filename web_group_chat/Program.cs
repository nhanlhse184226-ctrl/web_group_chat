namespace web_group_chat
{
    using web_group_chat.Hubs;
    using web_group_chat.Repositories;
    using web_group_chat.Services;

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IChatUserRepository, InMemoryChatUserRepository>();
            builder.Services.AddScoped<IFileStorageRepository, LocalFileStorageRepository>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IFileUploadService, FileUploadService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapRazorPages();
            app.MapHub<ChatHub>("/chatHub");

            app.Run();
        }
    }
}
