using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonToPdf.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PdfController : ControllerBase
    {
        private readonly ILogger<PdfController> _logger;
        private readonly AppDbContext _context;
        private readonly DynamicJsonToPdfConverter _converter;
        private readonly ErrorLogger _errorLogger;

        public PdfController(ILogger<PdfController> logger, AppDbContext context, DynamicJsonToPdfConverter converter, IWebHostEnvironment env)
        {
            _logger = logger;
            _context = context;
            _converter = converter;
            _errorLogger = new ErrorLogger(Path.Combine(env.ContentRootPath, "Logs"));
        }

        [HttpPost("convert")]
        public async Task<IActionResult> ConvertJsonToPdf(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is empty");

            if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return BadRequest("File is not a JSON file");

            try
            {
                _logger.LogInformation("Starting JSON to PDF conversion");

                string jsonContent;
                using (var streamReader = new StreamReader(file.OpenReadStream()))
                {
                    jsonContent = await streamReader.ReadToEndAsync();
                }

                JObject json;
                try
                {
                    json = JObject.Parse(jsonContent);
                }
                catch (JsonReaderException ex)
                {
                    _logger.LogError(ex, "Error parsing JSON");
                    _errorLogger.LogError(ex, "Error parsing JSON");
                    return BadRequest("Invalid JSON format");
                }

                byte[] pdfBytes;
                try
                {
                    
                    string jsonString = json.ToString();
                    pdfBytes = _converter.ConvertJsonToPdf(jsonString);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error converting JSON to PDF");
                    _errorLogger.LogError(ex, "Error converting JSON to PDF");
                    return StatusCode(500, "Error converting JSON to PDF");
                }


                var fileName = Path.GetFileNameWithoutExtension(file.FileName) + ".pdf";

                _logger.LogInformation($"PDF created with filename: {fileName}");

                var pdfDocument = new PdfDocument
                {
                    FileName = fileName,
                    Content = pdfBytes,
                    CreatedAt = DateTime.UtcNow
                };

                try
                {
                    _context.PdfDocuments.Add(pdfDocument);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"PDF saved to database with ID: {pdfDocument.Id}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving PDF to database");
                    _errorLogger.LogError(ex, "Error saving PDF to database");
                    return StatusCode(500, "Error saving PDF to database");
                }

                return Ok(new { Id = pdfDocument.Id, FileName = pdfDocument.FileName, Message = "PDF created successfully. Use this ID to retrieve the PDF." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while converting JSON to PDF");
                _errorLogger.LogError(ex, "Unexpected error occurred while converting JSON to PDF");
                return StatusCode(500, "An unexpected error occurred. Please try again later.");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPdf(int id)
        {
            var pdfDocument = await _context.PdfDocuments.FindAsync(id);
            if (pdfDocument == null)
            {
                _logger.LogWarning($"PDF with ID {id} not found");
                return NotFound("PDF not found.");
            }

            _logger.LogInformation($"Retrieving PDF with ID: {id}");
            return File(pdfDocument.Content, "application/pdf", pdfDocument.FileName);
        }

        [HttpDelete("cleanup")]
        public async Task<IActionResult> CleanupOldPdfs()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-30);
                var oldPdfs = await _context.PdfDocuments.Where(p => p.CreatedAt < cutoffDate).ToListAsync();

                _logger.LogInformation($"Found {oldPdfs.Count} old PDFs to delete");

                _context.PdfDocuments.RemoveRange(oldPdfs);
                var deletedCount = await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully deleted {deletedCount} old PDF documents");
                return Ok($"Cleaned up {deletedCount} old PDF documents");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up old PDFs");
                _errorLogger.LogError(ex, "Error occurred while cleaning up old PDFs");
                return StatusCode(500, "An error occurred while cleaning up old PDFs");
            }
        }

        [HttpGet("errors")]
        public IActionResult GetErrors()
        {
            var errors = _context.ErrorLogs.OrderByDescending(e => e.Timestamp).Take(100).ToList();
            return Ok(errors);
        }

        private string GetFileNameFromJson(JObject json)
        {
            string fileName = json["invoice"]?["invoiceNumber"]?.ToString() ??
                              json["invoice"]?["orderNumber"]?.ToString() ??
                              "Invoice";
            return $"{fileName}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
        }
    }
}
