using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace БД
{
    public class RecordWindow : Window
    {
        private DataTable schemaTable;
        private DataRow existingRow;
        private Dictionary<string, TextBox> inputControls = new Dictionary<string, TextBox>();

        public Dictionary<string, object> ResultData { get; private set; }

        public RecordWindow(DataTable schemaTable, DataRow existingRow = null)
        {
            this.schemaTable = schemaTable;
            this.existingRow = existingRow;

            Title = existingRow == null ? "Добавление записи" : "Редактирование записи";
            Width = 400;
            Height = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            InitUI();
        }

        private void InitUI()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var stackPanel = new StackPanel { Margin = new Thickness(15) };
            var scrollViewer = new ScrollViewer { Content = stackPanel };

            foreach (DataRow col in schemaTable.Rows)
            {
                string colName = col["ColumnName"].ToString();
                bool isAutoIncrement = col["IsAutoIncrement"] != DBNull.Value && (bool)col["IsAutoIncrement"];
                bool isKey = col["IsKey"] != DBNull.Value && (bool)col["IsKey"];

                var lbl = new TextBlock
                {
                    Text = colName,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 2)
                };
                stackPanel.Children.Add(lbl);

                var txtBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };

                if (existingRow != null && existingRow.Table.Columns.Contains(colName))
                {
                    txtBox.Text = existingRow[colName]?.ToString();
                }

                if (isAutoIncrement && existingRow == null)
                {
                    txtBox.Text = "(Заполняется автоматически)";
                    txtBox.IsEnabled = false;
                }
                else if (isKey && existingRow != null)
                {
                    txtBox.IsEnabled = false;
                }

                inputControls[colName] = txtBox;
                stackPanel.Children.Add(txtBox);
            }

            Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(15)
            };

            var btnSave = new Button { Content = "Сохранить", Width = 90, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button { Content = "Отмена", Width = 90, Height = 30 };
            btnCancel.Click += (s, e) => DialogResult = false;

            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnCancel);

            Grid.SetRow(btnPanel, 1);
            mainGrid.Children.Add(btnPanel);

            Content = mainGrid;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ResultData = new Dictionary<string, object>();
            foreach (var kvp in inputControls)
            {
                if (kvp.Value.IsEnabled)
                {
                    string text = kvp.Value.Text.Trim();
                    ResultData[kvp.Key] = string.IsNullOrEmpty(text) ? DBNull.Value : (object)text;
                }
            }
            DialogResult = true;
        }
    }
}