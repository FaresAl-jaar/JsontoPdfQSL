using DevLab.JmesPath;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace JsonToPdf
{
    public class DynamicJsonToPdfConverter
    {
        private readonly AppDbContext _context;
        private readonly string _logoPath;
        private readonly JmesPath _jmesPath;

        public DynamicJsonToPdfConverter(AppDbContext context, string logoPath)
        {
            _context = context;
            _logoPath = logoPath;
            _jmesPath = new JmesPath();
        }

        public byte[] ConvertJsonToPdf(string jsonString)
        {
            try
            {
                var json = JObject.Parse(jsonString);
                string docType = json["docType"]?.ToString() ?? json["data"]?["docType"]?.ToString();

                if (docType == "supplier")
                {
                    return ConvertSupplierInvoiceToPdf(json);
                }
                else
                {
                    return ConvertGenericInvoiceToPdf(json);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                throw;
            }
        }

        private byte[] ConvertSupplierInvoiceToPdf(JObject json)
        {
            var header = json["data"] ?? json;
            var invoiceNumber = header["number"]?.ToString() ?? "Invoice";

            using (var ms = new MemoryStream())
            {
                using (var document = new Document(PageSize.A4, 36, 36, 54, 36))
                {
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    AddLogo(document);
                    AddSupplierInvoiceHeader(document, header);
                    AddSupplierInvoiceDetailsTable(document, header);
                    AddSupplierInvoiceTotals(document, header);
                    document.Close();
                }
                return ms.ToArray();
            }
        }

        private void AddSupplierInvoiceHeader(Document document, JToken header)
        {
            Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
            Font normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);

            document.Add(new Paragraph("INVOICE", titleFont) { Alignment = Element.ALIGN_CENTER });
            document.Add(new Paragraph("\n"));

            PdfPTable table = new PdfPTable(2);
            table.WidthPercentage = 100;

            PdfPCell leftCell = new PdfPCell();
            leftCell.AddElement(new Paragraph($"Name: {header["name"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Vendor No: {header["vendor_no"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Invoice Number: {header["number"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Invoice Date: {header["invoice_date"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Order Number: {header["order_number"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Currency: {header["currency"]}", normalFont));
            leftCell.Border = Rectangle.NO_BORDER;

            PdfPCell rightCell = new PdfPCell();
            Paragraph companyInfo = GetHardcodedStyleParagraph();
            rightCell.AddElement(companyInfo);
            rightCell.Border = Rectangle.NO_BORDER;
            rightCell.VerticalAlignment = Element.ALIGN_BOTTOM;

            table.AddCell(leftCell);
            table.AddCell(rightCell);

            document.Add(table);
            document.Add(new Paragraph("\n"));
        }

        private void AddSupplierInvoiceDetailsTable(Document document, JToken details)
        {
            PdfPTable table = new PdfPTable(2);
            table.WidthPercentage = 100;
            table.SpacingBefore = 10f;
            table.SpacingAfter = 10f;
            table.HorizontalAlignment = Element.ALIGN_LEFT;

            Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
            Font cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

            table.AddCell(new PdfPCell(new Phrase("Field", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, HorizontalAlignment = Element.ALIGN_CENTER, Border = Rectangle.BOX });
            table.AddCell(new PdfPCell(new Phrase("Value", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, HorizontalAlignment = Element.ALIGN_CENTER, Border = Rectangle.BOX });

            var fields = new string[] {
                "barcode", "receipt_date", "delivery_date", "vat_id_no", "invoice_date", "valuta_date",
                "compliance_comments", "credit_note", "number", "vendor_no", "creditor_no", "addressgroup_no",
                "name", "prepay", "net_amount1", "tax_rate1", "tax_amount1",
                "order_number", "inv_reference_number", "inv_reference_date", "currency", "net_sum", "gross_amount"
            };

            foreach (var field in fields)
            {
                var value = details[field]?.ToString();
                if (value == null)
                {
                    value = "";
                }

                if (value == "0,00" || value == "0.00")
                {
                    continue;
                }

                var cell = new PdfPCell(new Phrase(field, cellFont)) { Border = Rectangle.BOX };
                var valueCell = new PdfPCell(new Phrase(value, cellFont)) { Border = Rectangle.BOX };

                table.AddCell(cell);
                table.AddCell(valueCell);
            }

            document.Add(table);
            document.Add(new Paragraph("\n"));
        }

        private void AddSupplierInvoiceTotals(Document document, JToken header)
        {
            Font boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
            Font normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            PdfPTable table = new PdfPTable(2);
            table.WidthPercentage = 30;
            table.HorizontalAlignment = Element.ALIGN_RIGHT;

            table.AddCell(new PdfPCell(new Phrase("Net Amount:", boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
            table.AddCell(new PdfPCell(new Phrase(header["net_amount1"].ToString(), normalFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });

            table.AddCell(new PdfPCell(new Phrase("Tax Amount:", boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
            table.AddCell(new PdfPCell(new Phrase(header["tax_amount1"].ToString(), normalFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });

            table.AddCell(new PdfPCell(new Phrase("Net Sum:", boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
            table.AddCell(new PdfPCell(new Phrase(header["net_sum"].ToString(), normalFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });

            table.AddCell(new PdfPCell(new Phrase("Gross Amount:", boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
            table.AddCell(new PdfPCell(new Phrase(header["gross_amount"].ToString(), boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });

            document.Add(table);
            document.Add(new Paragraph("\n\n"));
        }

        private byte[] ConvertGenericInvoiceToPdf(JObject json)
        {
            var invoiceHeader = _jmesPath.Transform(json, "invoice.{customer: customer, store: store, invoiceNumber: invoiceNumber, invoiceDate: invoiceDateTime, orderNumber: orderNumber, deliveryNoteNumber: deliveryNoteNumber, invoiceType: invoiceType}") as JObject;
            var invoiceTotals = _jmesPath.Transform(json, "invoice.{netAmount: totalNetAmount, taxAmount: totalTaxAmount, grossAmount: totalGrossAmount}") as JObject;
            var invoiceItems = _jmesPath.Transform(json, "invoice.items[*].{item: item, description: itemDescription, quantity: itemQuantity, unitPrice: itemUnitNetAmount, netAmount: itemNetAmount, taxRate: itemTaxRate}") as JArray;

            if (invoiceHeader == null || invoiceTotals == null || invoiceItems == null)
            {
                throw new InvalidOperationException("Failed to parse JSON data");
            }

            using (var ms = new MemoryStream())
            {
                using (var document = new Document(PageSize.A4, 36, 36, 54, 36))
                {
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    document.Open();

                    AddLogo(document);
                    AddInvoiceHeader(document, invoiceHeader);
                    AddInvoiceItems(document, invoiceItems);
                    AddInvoiceTotals(document, invoiceTotals);

                    document.Close();
                }
                return ms.ToArray();
            }
        }

        private void AddLogo(Document document)
        {
            try
            {
                if (File.Exists(_logoPath))
                {
                    Image logo = Image.GetInstance(_logoPath);
                    float maxWidth = document.PageSize.Width * 0.2f;
                    float maxHeight = document.PageSize.Height * 0.1f;
                    logo.ScaleToFit(maxWidth, maxHeight);

                    float topMargin = 10f;
                    float rightMargin = 36f;

                    float yPosition = document.PageSize.Height - logo.ScaledHeight - topMargin;
                    float xPosition = document.PageSize.Width - logo.ScaledWidth - rightMargin;

                    logo.SetAbsolutePosition(xPosition, yPosition);
                    document.Add(logo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding logo: {ex.Message}");
            }
        }

        private void AddInvoiceHeader(Document document, JObject header)
        {
            Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
            Font normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);

            document.Add(new Paragraph("INVOICE", titleFont) { Alignment = Element.ALIGN_CENTER });
            document.Add(new Paragraph("\n"));

            document.Add(new Paragraph(" ") { SpacingAfter = 20f });

            PdfPTable table = new PdfPTable(2);
            table.WidthPercentage = 100;

            PdfPCell leftCell = new PdfPCell();
            leftCell.AddElement(new Paragraph($"Customer: {header["customer"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Store: {header["store"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Invoice Number: {header["invoiceNumber"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Invoice Date: {header["invoiceDate"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Order Number: {header["orderNumber"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Delivery Note Number: {header["deliveryNoteNumber"]}", normalFont));
            leftCell.AddElement(new Paragraph($"Invoice Type: {header["invoiceType"]}", normalFont));
            leftCell.Border = Rectangle.NO_BORDER;

            PdfPCell rightCell = new PdfPCell();
            Paragraph companyInfo = GetHardcodedStyleParagraph();
            rightCell.AddElement(companyInfo);
            rightCell.Border = Rectangle.NO_BORDER;
            rightCell.VerticalAlignment = Element.ALIGN_BOTTOM;

            table.AddCell(leftCell);
            table.AddCell(rightCell);

            document.Add(table);
            document.Add(new Paragraph("\n"));
        }

        private Paragraph GetHardcodedStyleParagraph()
        {
            Font normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            Font boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);

            Paragraph hardcodedStyle = new Paragraph();
            hardcodedStyle.Add(new Chunk("Meyer Quick Service Logistics GmbH & Co. KG\n", boldFont));
            hardcodedStyle.Add(new Chunk("Ludwig-Meyer-Str. 2-4\n", normalFont));
            hardcodedStyle.Add(new Chunk("61381 Friedrichsdorf\n", normalFont));
            hardcodedStyle.Add(new Chunk("- Germany -\n\n", normalFont));
            hardcodedStyle.Add(new Chunk("Tel. +49 (0) 6175 / 4009-423\n", normalFont));
            hardcodedStyle.Add(new Chunk("Fax +49 (0) 6175 / 4009-431\n", normalFont));
            hardcodedStyle.Add(new Chunk("www.quick-service-logistics.de\n", normalFont));

            hardcodedStyle.Alignment = Element.ALIGN_RIGHT;

            return hardcodedStyle;
        }

        private void AddInvoiceItems(Document document, JArray items)
        {
            PdfPTable table = new PdfPTable(6);
            table.WidthPercentage = 100;
            table.SetWidths(new float[] { 1f, 4f, 1f, 1f, 1f, 1f });

            Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
            Font cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            string[] headers = { "Item", "Description", "Quantity", "Unit Price", "Net Amount", "Tax Rate" };
            foreach (string header in headers)
            {
                table.AddCell(new PdfPCell(new Phrase(header, headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY, HorizontalAlignment = Element.ALIGN_CENTER });
            }

            foreach (var item in items)
            {
                table.AddCell(new PdfPCell(new Phrase(item["item"].ToString(), cellFont)));
                table.AddCell(new PdfPCell(new Phrase(item["description"].ToString(), cellFont)));
                table.AddCell(new PdfPCell(new Phrase(item["quantity"].ToString(), cellFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });
                table.AddCell(new PdfPCell(new Phrase(item["unitPrice"].ToString(), cellFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });
                table.AddCell(new PdfPCell(new Phrase(item["netAmount"].ToString(), cellFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });
                table.AddCell(new PdfPCell(new Phrase(item["taxRate"].ToString(), cellFont)) { HorizontalAlignment = Element.ALIGN_RIGHT });
            }

            document.Add(table);
            document.Add(new Paragraph("\n"));
        }

        private void AddInvoiceTotals(Document document, JObject totals)
        {
            Font boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9);
            Font normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

            PdfPTable table = new PdfPTable(2);
            table.WidthPercentage = 30;
            table.HorizontalAlignment = Element.ALIGN_RIGHT;

            table.AddCell(new PdfPCell(new Phrase("Net Amount:", boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
            table.AddCell(new PdfPCell(new Phrase(totals["netAmount"].ToString(), normalFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });

            table.AddCell(new PdfPCell(new Phrase("Tax Amount:", boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
            table.AddCell(new PdfPCell(new Phrase(totals["taxAmount"].ToString(), normalFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });

            table.AddCell(new PdfPCell(new Phrase("Net Sum:", boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
            table.AddCell(new PdfPCell(new Phrase(totals["netSum"].ToString(), normalFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });

            table.AddCell(new PdfPCell(new Phrase("Gross Amount:", boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_LEFT });
            table.AddCell(new PdfPCell(new Phrase(totals["grossAmount"].ToString(), boldFont)) { Border = Rectangle.NO_BORDER, HorizontalAlignment = Element.ALIGN_RIGHT });

            document.Add(table);
            document.Add(new Paragraph("\n\n"));
        }

        private void LogError(Exception ex)
        {
            var errorLog = new ErrorLog
            {
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace,
                Timestamp = DateTime.UtcNow
            };

            _context.ErrorLogs.Add(errorLog);
            _context.SaveChanges();
        }
    }
}

