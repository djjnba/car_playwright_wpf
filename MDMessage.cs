using System.Threading.Tasks;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

public static class MDMessage
{
    /// <summary>
    /// 显示 Material Design 风格的提示框
    /// </summary>
    public static async Task Show(string message, string title = "")
    {
        var stack = new StackPanel { Margin = new System.Windows.Thickness(20) };

        if (!string.IsNullOrEmpty(title))
        {
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 14,
            TextWrapping = System.Windows.TextWrapping.Wrap
        });

        await DialogHost.Show(stack, "RootDialog");
    }
}
