﻿using Bhp.Properties;
using Bhp.Server;
using Bhp.Wallets;
using System;
using System.Linq;
using System.Windows.Forms;

namespace Bhp.UI
{
    internal partial class PayToDialog : Form
    {
        public PayToDialog(WalletAssetDescriptor asset = null, UInt160 scriptHash = null)
        {
            InitializeComponent();
            if (asset == null)
            {
                foreach (var balance in Program.MainForm.CurrentBalances)
                {
                    comboBox1.Items.Add(new WalletAssetDescriptor(balance.Key));
                }
                foreach (string s in Settings.Default.BRC20Watched)
                {
                    UInt160 asset_id = UInt160.Parse(s);
                    try
                    {
                        comboBox1.Items.Add(new WalletAssetDescriptor(asset_id));
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }
            }
            else
            {
                comboBox1.Items.Add(asset);
                comboBox1.SelectedIndex = 0;
                comboBox1.Enabled = false;
            }
            if (scriptHash != null)
            {
                textBox1.Text = scriptHash.ToAddress();
                textBox1.ReadOnly = true;
            }
        }

        public TxOutListBoxItem GetOutput()
        {
            WalletAssetDescriptor asset = (WalletAssetDescriptor)comboBox1.SelectedItem;
            return new TxOutListBoxItem
            {
                AssetName = asset.AssetName,
                AssetId = asset.AssetId,
                Value = BigDecimal.Parse(textBox2.Text, 8),
                ScriptHash = textBox1.Text.ToScriptHash()
            };
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem is WalletAssetDescriptor asset)
            {
                textBox3.Text = Program.MainForm.CurrentBalances[(UInt256)asset.AssetId].ToString();
            }
            else
            {
                textBox3.Text = "";
            }
            textBox_TextChanged(this, EventArgs.Empty);
        }

        private void textBox_TextChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex < 0 || textBox1.TextLength == 0 || textBox2.TextLength == 0)
            {
                button1.Enabled = false;
                return;
            }
            try
            {
                textBox1.Text.ToScriptHash();
            }
            catch (FormatException)
            {
                button1.Enabled = false;
                return;
            }
            WalletAssetDescriptor asset = (WalletAssetDescriptor)comboBox1.SelectedItem;
            if (!BigDecimal.TryParse(textBox2.Text, asset.Decimals, out BigDecimal amount))
            {
                button1.Enabled = false;
                return;
            }
            if (amount.Sign <= 0)
            {
                button1.Enabled = false;
                return;
            }
            button1.Enabled = true;
        }
    }
}
