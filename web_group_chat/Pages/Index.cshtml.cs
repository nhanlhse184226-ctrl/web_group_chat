using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace web_group_chat.Pages
{
    public class IndexModel : PageModel
    {
        const long MaxUploadBytes = 25 * 1024 * 1024;
        private readonly ILogger<IndexModel> _logger;
        private readonly IWebHostEnvironment _environment;

        public IndexModel(ILogger<IndexModel> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public void OnGet()
        {

        }

        public async Task<IActionResult> OnPostUploadFileAsync(IFormFile? chatFile)
        {
            if (chatFile == null || chatFile.Length == 0)
                return BadRequest(new { error = "Choose a file first." });

            if (chatFile.Length > MaxUploadBytes)
                return BadRequest(new { error = "File must be 25 MB or smaller." });

            string uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsRoot);

            string originalName = Path.GetFileName(chatFile.FileName);
            string extension = Path.GetExtension(originalName);
            string storedName = $"{Guid.NewGuid():N}{extension}";
            string storedPath = Path.Combine(uploadsRoot, storedName);

            await using (var fileStream = System.IO.File.Create(storedPath))
            {
                await chatFile.CopyToAsync(fileStream);
            }

            _logger.LogInformation("Uploaded chat file {FileName} ({FileSize} bytes).", originalName, chatFile.Length);

            return new JsonResult(new
            {
                fileName = originalName,
                fileSize = chatFile.Length,
                fileUrl = Url.Content($"~/uploads/{storedName}")
            });
        }
    }
}
