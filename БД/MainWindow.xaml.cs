using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace БД
{
    public partial class MainWindow : Window
    {
        private DataTable currentDataTable;
        private readonly string baseConnString = "Server=imicgmanly.ru;Uid=galin;Pwd=12345678;Charset=utf8mb4;SslMode=Disabled;AllowPublicKeyRetrieval=True;"; public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDatabases();
        }

        private void LoadDatabases()
        {
            try
            {
                using (var conn = new MySqlConnection(baseConnString))
                {
                    conn.Open();
                    var cmd = new MySqlCommand("SHOW DATABASES;", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        var dbs = new List<string>();
                        while (reader.Read())
                        {
                            string db = reader.GetString(0);
                            if (!new[] { "information_schema", "performance_schema", "mysql", "sys" }.Contains(db))
                            {
                                dbs.Add(db);
                            }
                        }
                        CmbDatabases.ItemsSource = dbs;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к серверу: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbDatabases_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbDatabases.SelectedItem == null) return;
            string dbName = CmbDatabases.SelectedItem.ToString();
            DgData.ItemsSource = null;
            CmbTables.ItemsSource = null;

            try
            {
                using (var conn = new MySqlConnection($"{baseConnString}Database={dbName};"))
                {
                    conn.Open();
                    var cmd = new MySqlCommand("SHOW TABLES;", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        var tables = new List<string>();
                        while (reader.Read())
                        {
                            tables.Add(reader.GetString(0));
                        }
                        CmbTables.ItemsSource = tables;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблиц: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadTableData();
        }

        private string GetCurrentConnString()
        {
            if (CmbDatabases.SelectedItem == null) return baseConnString;
            return $"{baseConnString}Database={CmbDatabases.SelectedItem};";
        }

        private void LoadTableData()
        {
            if (CmbTables.SelectedItem == null) return;
            string tableName = CmbTables.SelectedItem.ToString();

            try
            {
                using (var conn = new MySqlConnection(GetCurrentConnString()))
                {
                    conn.Open();
                    var da = new MySqlDataAdapter($"SELECT * FROM `{tableName}`;", conn);
                    currentDataTable = new DataTable();
                    da.Fill(currentDataTable);
                    DgData.ItemsSource = currentDataTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DataTable GetTableSchema(string tableName)
        {
            using (var conn = new MySqlConnection(GetCurrentConnString()))
            {
                conn.Open();
                var cmd = new MySqlCommand($"SELECT * FROM `{tableName}` LIMIT 0;", conn);
                using (var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo))
                {
                    return reader.GetSchemaTable();
                }
            }
        }

        private string GetPrimaryKeyColumn(DataTable schemaTable)
        {
            foreach (DataRow row in schemaTable.Rows)
            {
                if (row["IsKey"] != DBNull.Value && (bool)row["IsKey"])
                    return row["ColumnName"].ToString();
            }
            return schemaTable.Rows[0]["ColumnName"].ToString();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTables.SelectedItem == null) return;
            string tableName = CmbTables.SelectedItem.ToString();

            var schema = GetTableSchema(tableName);
            var win = new RecordWindow(schema) { Owner = this };

            if (win.ShowDialog() == true && win.ResultData.Count > 0)
            {
                var keys = win.ResultData.Keys.ToList();
                var cols = string.Join(", ", keys.Select(k => $"`{k}`"));
                var paramNames = string.Join(", ", keys.Select((k, idx) => $"@p{idx}"));
                string sql = $"INSERT INTO `{tableName}` ({cols}) VALUES ({paramNames});";

                try
                {
                    using (var conn = new MySqlConnection(GetCurrentConnString()))
                    {
                        conn.Open();
                        var cmd = new MySqlCommand(sql, conn);
                        for (int i = 0; i < keys.Count; i++)
                        {
                            cmd.Parameters.AddWithValue($"@p{i}", win.ResultData[keys[i]]);
                        }
                        cmd.ExecuteNonQuery();
                    }
                    LoadTableData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка добавления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (DgData.SelectedItem == null)
            {
                MessageBox.Show("Сначала кликните по нужной строке в таблице.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DataRowView rowView = (DataRowView)DgData.SelectedItem;
            DataRow row = rowView.Row;
            string tableName = CmbTables.SelectedItem.ToString();

            var schema = GetTableSchema(tableName);
            string pkCol = GetPrimaryKeyColumn(schema);
            object pkVal = row[pkCol];

            var win = new RecordWindow(schema, row) { Owner = this };

            if (win.ShowDialog() == true && win.ResultData.Count > 0)
            {
                var keys = win.ResultData.Keys.ToList();
                var setClause = string.Join(", ", keys.Select((k, idx) => $"`{k}` = @p{idx}"));
                string sql = $"UPDATE `{tableName}` SET {setClause} WHERE `{pkCol}` = @pkVal;";

                try
                {
                    using (var conn = new MySqlConnection(GetCurrentConnString()))
                    {
                        conn.Open();
                        var cmd = new MySqlCommand(sql, conn);
                        for (int i = 0; i < keys.Count; i++)
                        {
                            cmd.Parameters.AddWithValue($"@p{i}", win.ResultData[keys[i]]);
                        }
                        cmd.Parameters.AddWithValue("@pkVal", pkVal);
                        cmd.ExecuteNonQuery();
                    }
                    LoadTableData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (DgData.SelectedItem == null)
            {
                MessageBox.Show("Сначала кликните по нужной строке в таблице.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show("Удалить выбранную запись?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView rowView = (DataRowView)DgData.SelectedItem;
                DataRow row = rowView.Row;
                string tableName = CmbTables.SelectedItem.ToString();

                var schema = GetTableSchema(tableName);
                string pkCol = GetPrimaryKeyColumn(schema);
                object pkVal = row[pkCol];

                string sql = $"DELETE FROM `{tableName}` WHERE `{pkCol}` = @pkVal;";

                try
                {
                    using (var conn = new MySqlConnection(GetCurrentConnString()))
                    {
                        conn.Open();
                        var cmd = new MySqlCommand(sql, conn);
                        cmd.Parameters.AddWithValue("@pkVal", pkVal);
                        cmd.ExecuteNonQuery();
                    }
                    LoadTableData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}