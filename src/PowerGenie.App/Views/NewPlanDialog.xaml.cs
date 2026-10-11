using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PowerGenie.App.Models;
using PowerGenie.App.Services;

namespace PowerGenie.App.Views;

public partial class NewPlanDialog : Window
{
    private readonly PowerPlanService _powerPlanService;

    public PowerPlan? CreatedPlan { get; private set; }

    public NewPlanDialog(List<PowerPlan> availablePlans, PowerPlanService powerPlanService)
    {
        InitializeComponent();
        _powerPlanService = powerPlanService;

        SourcePlanComboBox.ItemsSource = availablePlans;
        SourcePlanComboBox.SelectedItem = availablePlans.FirstOrDefault(p => p.IsActive)
            ?? availablePlans.FirstOrDefault();
    }

    private void PlanNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        CreateButton.IsEnabled = SourcePlanComboBox.SelectedItem is not null
            && PowerPlanService.ValidatePlanName(PlanNameTextBox.Text) is null;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourcePlanComboBox.SelectedItem is not PowerPlan source)
        {
            return;
        }

        try
        {
            CreatedPlan = _powerPlanService.DuplicatePlan(source.Guid, PlanNameTextBox.Text);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // Stay open so the user can adjust the name or source and try again.
            ErrorText.Text = $"Couldn't create the power plan.\n{ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    // Opens the classic Power Options control panel, where "Change plan settings" and then
    // "Change advanced power settings" edit any plan, the new one included. Deliberately not the
    // "rundll32 ... powercfg.cpl,,3" shortcut: that jumps straight into the advanced settings
    // of whichever plan is currently active, which is rarely the one just created.
    private void OpenPowerOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, "control.exe"), "powercfg.cpl")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ErrorText.Text = $"Couldn't open Windows Power Options.\n{ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
