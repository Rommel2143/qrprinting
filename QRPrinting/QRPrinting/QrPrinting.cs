using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace QRPrinting
{
    public partial class QrPrinting : Form
    {
        private const int Columns = 5;

        // ==========================================
        // LABEL SETTINGS
        // ==========================================

        private const float LabelSizeMm = 37.0f;
        private const float LabelGapMm = 2.0f;

        // QR = exactly 1 inch
        private const float QrSizeMm = 25.4f;

        // A4 margin
        private const float PageMarginMm = 5.0f;

        // Hole
        private const float HoleAreaMm = 5.0f;
        private const float HoleWidthMm = 8.0f;
        private const float HoleHeightMm = 1.8f;

        // Employee name
        private const float NameFontSize = 5.5f;

        // ==========================================
        // FONT
        // ==========================================

        private const string FontName = "Arial";

        public QrPrinting()
        {
            InitializeComponent();

            QuestPDF.Settings.License =
                LicenseType.Community;

            // Use fonts installed in Windows.
            // This allows QuestPDF to find Arial.
            QuestPDF.Settings.UseSystemFonts = true;

            // Do not allow the missing default Lato
            // font to stop PDF generation.
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
        }

        // ==========================================
        // MM TO POINTS
        // ==========================================

        private static float Mm(float millimeters)
        {
            return millimeters * 72f / 25.4f;
        }

        // ==========================================
        // PRINT
        // ==========================================

        private void btnPrint_Click(object sender, EventArgs e)
        {
            try
            {
                List<string> employees =
                    txtEmployeeList.Lines
                        .Select(line => line.Trim())
                        .Where(line =>
                            !string.IsNullOrWhiteSpace(line))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                if (employees.Count == 0)
                {
                    MessageBox.Show(
                        "Please enter at least one employee name.",
                        "No Employees",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    txtEmployeeList.Focus();

                    return;
                }

                byte[] pdfBytes =
                    CreateEmployeeQrPdf(employees);

                OpenPdf(pdfBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to create QR PDF.\n\n" +
                    ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ==========================================
        // CREATE PDF
        // ==========================================

        private byte[] CreateEmployeeQrPdf(
            List<string> employees)
        {
            List<EmployeeQr> qrEmployees =
                employees
                    .Select(name => new EmployeeQr
                    {
                        Name = name,
                        QrCode = GenerateQrCode(name)
                    })
                    .ToList();

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    // A4
                    page.Size(PageSizes.A4);

                    // Margin
                    page.Margin(
                        Mm(PageMarginMm));

                    // IMPORTANT:
                    // Explicitly make Arial the default
                    // document font.
                    page.DefaultTextStyle(
                        textStyle =>
                            textStyle
                                .FontFamily(FontName)
                                .FontSize(8));

                    page.Content()
                        .Grid(grid =>
                        {
                            grid.Columns(Columns);

                            grid.HorizontalSpacing(
                                Mm(LabelGapMm));

                            grid.VerticalSpacing(
                                Mm(LabelGapMm));

                            foreach (
                                EmployeeQr employee
                                in qrEmployees)
                            {
                                grid.Item()
                                    .Element(container =>
                                        CreateQrLabel(
                                            container,
                                            employee));
                            }
                        });
                });
            })
            .GeneratePdf();
        }

        // ==========================================
        // CREATE LABEL
        // ==========================================

        private void CreateQrLabel(
            IContainer container,
            EmployeeQr employee)
        {
            float labelSize =
                Mm(LabelSizeMm);

            float qrSize =
                Mm(QrSizeMm);

            float holeArea =
                Mm(HoleAreaMm);

            float holeWidth =
                Mm(HoleWidthMm);

            float holeHeight =
                Mm(HoleHeightMm);

            container
                .Width(labelSize)
                .Height(labelSize)

                // Cutting border
                .Border(Mm(0.35f))
                .BorderColor(
                    Colors.Grey.Darken1)

                // Center everything
                .AlignCenter()
                .AlignMiddle()

                .Column(column =>
                {
                    // ======================================
                    // LONG HORIZONTAL HOLE
                    // ======================================

                    column.Item()
                        .Height(holeArea)
                        .AlignCenter()
                        .AlignMiddle()
                        .Width(holeWidth)
                        .Height(holeHeight)
                        .Border(Mm(0.35f))
                        .BorderColor(
                            Colors.Grey.Darken1)
                        .CornerRadius(
                            Mm(0.9f));

                    // ======================================
                    // QR CODE
                    // ======================================

                    column.Item()
                        .Width(qrSize)
                        .Height(qrSize)
                        .AlignCenter()
                        .AlignMiddle()
                        .Image(employee.QrCode);

                    // ======================================
                    // EMPLOYEE NAME
                    // ======================================

                    column.Item()
                        .PaddingTop(Mm(1.0f))
                        .PaddingHorizontal(Mm(1.0f))
                        .AlignCenter()
                        .AlignMiddle()
                        .Text(employee.Name)
                        .FontFamily(FontName)
                        .Bold()
                        .FontSize(NameFontSize)
                        .LineHeight(1.0f);
                });
        }

        // ==========================================
        // GENERATE QR
        // ==========================================

        private byte[] GenerateQrCode(
            string employeeName)
        {
            using (QRCodeGenerator qrGenerator =
                   new QRCodeGenerator())
            {
                using (QRCodeData qrCodeData =
                       qrGenerator.CreateQrCode(
                           employeeName,
                           QRCodeGenerator.ECCLevel.Q))
                {
                    using (PngByteQRCode qrCode =
                           new PngByteQRCode(qrCodeData))
                    {
                        return qrCode.GetGraphic(
                            pixelsPerModule: 10);
                    }
                }
            }
        }

        // ==========================================
        // OPEN PDF
        // ==========================================

        private void OpenPdf(byte[] pdfBytes)
        {
            string tempFile =
                Path.Combine(
                    Path.GetTempPath(),
                    "EmployeeQr_" +
                    Guid.NewGuid().ToString("N") +
                    ".pdf");

            try
            {
                File.WriteAllBytes(
                    tempFile,
                    pdfBytes);

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = tempFile,
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to open PDF preview.\n\n" +
                    ex.Message,
                    "PDF Preview Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ==========================================
        // EMPLOYEE
        // ==========================================

        private class EmployeeQr
        {
            public string Name { get; set; }

            public byte[] QrCode { get; set; }
        }
    }
}