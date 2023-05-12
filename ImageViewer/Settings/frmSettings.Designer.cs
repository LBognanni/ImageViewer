using System.ComponentModel;

namespace ImageViewer;

partial class frmSettings
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmSettings));
        this.cmdOk = new System.Windows.Forms.Button();
        this.cmdCancel = new System.Windows.Forms.Button();
        this.groupBox1 = new System.Windows.Forms.GroupBox();
        this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
        this.cbFullScreen = new System.Windows.Forms.CheckBox();
        this.ddZoom = new System.Windows.Forms.ComboBox();
        this.cbTransparent = new System.Windows.Forms.CheckBox();
        this.label1 = new System.Windows.Forms.Label();
        this.groupBox1.SuspendLayout();
        this.tableLayoutPanel1.SuspendLayout();
        this.SuspendLayout();
        // 
        // cmdOk
        // 
        this.cmdOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.cmdOk.Location = new System.Drawing.Point(174, 125);
        this.cmdOk.Name = "cmdOk";
        this.cmdOk.Size = new System.Drawing.Size(88, 27);
        this.cmdOk.TabIndex = 0;
        this.cmdOk.Text = "&Ok";
        this.cmdOk.UseVisualStyleBackColor = true;
        // 
        // cmdCancel
        // 
        this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this.cmdCancel.Location = new System.Drawing.Point(268, 125);
        this.cmdCancel.Name = "cmdCancel";
        this.cmdCancel.Size = new System.Drawing.Size(88, 27);
        this.cmdCancel.TabIndex = 1;
        this.cmdCancel.Text = "&Cancel";
        this.cmdCancel.UseVisualStyleBackColor = true;
        // 
        // groupBox1
        // 
        this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this.groupBox1.Controls.Add(this.tableLayoutPanel1);
        this.groupBox1.Location = new System.Drawing.Point(12, 12);
        this.groupBox1.Name = "groupBox1";
        this.groupBox1.Size = new System.Drawing.Size(344, 107);
        this.groupBox1.TabIndex = 2;
        this.groupBox1.TabStop = false;
        this.groupBox1.Text = "Defaults";
        // 
        // tableLayoutPanel1
        // 
        this.tableLayoutPanel1.AutoSize = true;
        this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        this.tableLayoutPanel1.ColumnCount = 2;
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
        this.tableLayoutPanel1.Controls.Add(this.cbFullScreen, 0, 0);
        this.tableLayoutPanel1.Controls.Add(this.ddZoom, 1, 2);
        this.tableLayoutPanel1.Controls.Add(this.cbTransparent, 0, 1);
        this.tableLayoutPanel1.Controls.Add(this.label1, 0, 2);
        this.tableLayoutPanel1.Location = new System.Drawing.Point(6, 19);
        this.tableLayoutPanel1.Name = "tableLayoutPanel1";
        this.tableLayoutPanel1.RowCount = 3;
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this.tableLayoutPanel1.Size = new System.Drawing.Size(242, 73);
        this.tableLayoutPanel1.TabIndex = 4;
        // 
        // cbFullScreen
        // 
        this.cbFullScreen.AutoSize = true;
        this.tableLayoutPanel1.SetColumnSpan(this.cbFullScreen, 2);
        this.cbFullScreen.Location = new System.Drawing.Point(3, 3);
        this.cbFullScreen.Name = "cbFullScreen";
        this.cbFullScreen.Size = new System.Drawing.Size(110, 17);
        this.cbFullScreen.TabIndex = 0;
        this.cbFullScreen.Text = "Start in full screen";
        this.cbFullScreen.UseVisualStyleBackColor = true;
        // 
        // ddZoom
        // 
        this.ddZoom.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.ddZoom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.ddZoom.FormattingEnabled = true;
        this.ddZoom.Items.AddRange(new object[] { "Fit to screen", "Actual size" });
        this.ddZoom.Location = new System.Drawing.Point(81, 49);
        this.ddZoom.Name = "ddZoom";
        this.ddZoom.Size = new System.Drawing.Size(158, 21);
        this.ddZoom.TabIndex = 3;
        // 
        // cbTransparent
        // 
        this.cbTransparent.AutoSize = true;
        this.tableLayoutPanel1.SetColumnSpan(this.cbTransparent, 2);
        this.cbTransparent.Location = new System.Drawing.Point(3, 26);
        this.cbTransparent.Name = "cbTransparent";
        this.cbTransparent.Size = new System.Drawing.Size(236, 17);
        this.cbTransparent.TabIndex = 1;
        this.cbTransparent.Text = "Make window transparent when out of focus";
        this.cbTransparent.UseVisualStyleBackColor = true;
        // 
        // label1
        // 
        this.label1.Anchor = System.Windows.Forms.AnchorStyles.Left;
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(3, 53);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(72, 13);
        this.label1.TabIndex = 2;
        this.label1.Text = "Default zoom:";
        // 
        // frmSettings
        // 
        this.AcceptButton = this.cmdOk;
        this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.CancelButton = this.cmdCancel;
        this.ClientSize = new System.Drawing.Size(366, 162);
        this.Controls.Add(this.groupBox1);
        this.Controls.Add(this.cmdCancel);
        this.Controls.Add(this.cmdOk);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
        this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
        this.Name = "frmSettings";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Settings";
        this.groupBox1.ResumeLayout(false);
        this.groupBox1.PerformLayout();
        this.tableLayoutPanel1.ResumeLayout(false);
        this.tableLayoutPanel1.PerformLayout();
        this.ResumeLayout(false);
    }

    private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;

    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.ComboBox ddZoom;

    private System.Windows.Forms.CheckBox cbFullScreen;
    private System.Windows.Forms.CheckBox cbTransparent;

    private System.Windows.Forms.GroupBox groupBox1;

    private System.Windows.Forms.Button cmdOk;
    private System.Windows.Forms.Button cmdCancel;

    #endregion
}