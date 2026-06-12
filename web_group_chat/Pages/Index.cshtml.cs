using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using web_group_chat.Services;

namespace web_group_chat.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IFileUploadService fileUploadService;

        public IndexModel(IFileUploadService fileUploadService)
        {
            this.fileUploadService = fileUploadService;
        }

        public void OnGet()
        {

        }

        public async Task<IActionResult> OnPostUploadFileAsync(IFormFile? chatFile)
        {
            try
            {
                var uploadedFile = await fileUploadService.UploadAsync(chatFile, HttpContext.RequestAborted);
                return new JsonResult(uploadedFile);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
