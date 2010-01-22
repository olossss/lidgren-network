namespace SamplesCommon
{
	partial class NetPeerSettingsWindow
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

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
			this.DebugCheckBox = new System.Windows.Forms.CheckBox();
			this.VerboseCheckBox = new System.Windows.Forms.CheckBox();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.MinLatencyTextBox = new System.Windows.Forms.TextBox();
			this.LossTextBox = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.label4 = new System.Windows.Forms.Label();
			this.label5 = new System.Windows.Forms.Label();
			this.InfoLabel = new System.Windows.Forms.Label();
			this.button1 = new System.Windows.Forms.Button();
			this.groupBox1.SuspendLayout();
			this.groupBox2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.SuspendLayout();
			// 
			// DebugCheckBox
			// 
			this.DebugCheckBox.AutoSize = true;
			this.DebugCheckBox.Location = new System.Drawing.Point(6, 21);
			this.DebugCheckBox.Name = "DebugCheckBox";
			this.DebugCheckBox.Size = new System.Drawing.Size(61, 17);
			this.DebugCheckBox.TabIndex = 0;
			this.DebugCheckBox.Text = "Debug";
			this.DebugCheckBox.UseVisualStyleBackColor = true;
			this.DebugCheckBox.CheckedChanged += new System.EventHandler(this.DebugCheckBox_CheckedChanged);
			// 
			// VerboseCheckBox
			// 
			this.VerboseCheckBox.AutoSize = true;
			this.VerboseCheckBox.Location = new System.Drawing.Point(6, 44);
			this.VerboseCheckBox.Name = "VerboseCheckBox";
			this.VerboseCheckBox.Size = new System.Drawing.Size(105, 17);
			this.VerboseCheckBox.TabIndex = 1;
			this.VerboseCheckBox.Text = "Verbose debug";
			this.VerboseCheckBox.UseVisualStyleBackColor = true;
			this.VerboseCheckBox.CheckedChanged += new System.EventHandler(this.VerboseCheckBox_CheckedChanged);
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.label5);
			this.groupBox1.Controls.Add(this.label4);
			this.groupBox1.Controls.Add(this.textBox3);
			this.groupBox1.Controls.Add(this.label3);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.label1);
			this.groupBox1.Controls.Add(this.LossTextBox);
			this.groupBox1.Controls.Add(this.MinLatencyTextBox);
			this.groupBox1.Location = new System.Drawing.Point(12, 109);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(372, 85);
			this.groupBox1.TabIndex = 2;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Simulation";
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.DebugCheckBox);
			this.groupBox2.Controls.Add(this.VerboseCheckBox);
			this.groupBox2.Location = new System.Drawing.Point(12, 12);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(166, 91);
			this.groupBox2.TabIndex = 3;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Display messages";
			// 
			// groupBox3
			// 
			this.groupBox3.Controls.Add(this.InfoLabel);
			this.groupBox3.Location = new System.Drawing.Point(184, 12);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(281, 91);
			this.groupBox3.TabIndex = 4;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Info";
			// 
			// MinLatencyTextBox
			// 
			this.MinLatencyTextBox.Location = new System.Drawing.Point(103, 21);
			this.MinLatencyTextBox.Name = "MinLatencyTextBox";
			this.MinLatencyTextBox.Size = new System.Drawing.Size(54, 22);
			this.MinLatencyTextBox.TabIndex = 0;
			this.MinLatencyTextBox.TextChanged += new System.EventHandler(this.MinLatencyTextBox_TextChanged);
			// 
			// LossTextBox
			// 
			this.LossTextBox.Location = new System.Drawing.Point(103, 49);
			this.LossTextBox.Name = "LossTextBox";
			this.LossTextBox.Size = new System.Drawing.Size(54, 22);
			this.LossTextBox.TabIndex = 1;
			this.LossTextBox.TextChanged += new System.EventHandler(this.LossTextBox_TextChanged);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(6, 24);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(91, 13);
			this.label1.TabIndex = 2;
			this.label1.Text = "One way latency";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 52);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(29, 13);
			this.label2.TabIndex = 3;
			this.label2.Text = "Loss";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(163, 24);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(18, 13);
			this.label3.TabIndex = 4;
			this.label3.Text = "to";
			// 
			// textBox3
			// 
			this.textBox3.Location = new System.Drawing.Point(185, 21);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(54, 22);
			this.textBox3.TabIndex = 5;
			this.textBox3.TextChanged += new System.EventHandler(this.textBox3_TextChanged);
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(247, 24);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(21, 13);
			this.label4.TabIndex = 6;
			this.label4.Text = "ms";
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(163, 52);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(16, 13);
			this.label5.TabIndex = 7;
			this.label5.Text = "%";
			// 
			// InfoLabel
			// 
			this.InfoLabel.AutoSize = true;
			this.InfoLabel.Location = new System.Drawing.Point(6, 22);
			this.InfoLabel.Name = "InfoLabel";
			this.InfoLabel.Size = new System.Drawing.Size(38, 13);
			this.InfoLabel.TabIndex = 0;
			this.InfoLabel.Text = "label6";
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point(390, 114);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(75, 80);
			this.button1.TabIndex = 5;
			this.button1.Text = "Refresh";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.button1_Click);
			// 
			// NetPeerSettingsWindow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(474, 204);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.groupBox3);
			this.Controls.Add(this.groupBox2);
			this.Controls.Add(this.groupBox1);
			this.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.Name = "NetPeerSettingsWindow";
			this.Text = "NetPeerSettingsWindow1";
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.GroupBox groupBox3;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label3;
		public System.Windows.Forms.CheckBox DebugCheckBox;
		public System.Windows.Forms.CheckBox VerboseCheckBox;
		public System.Windows.Forms.TextBox LossTextBox;
		public System.Windows.Forms.TextBox MinLatencyTextBox;
		public System.Windows.Forms.TextBox textBox3;
		public System.Windows.Forms.Label InfoLabel;
		private System.Windows.Forms.Button button1;
	}
}