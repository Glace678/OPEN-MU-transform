// <copyright file="ConnectServerConfiguration.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Components.ConnectServer;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Edit component for the <see cref="ConnectServerDefinition"/>.
/// </summary>
/// <seealso cref="Microsoft.AspNetCore.Components.ComponentBase" />
public partial class ConnectServerConfiguration
{
    /// <summary>
    /// Gets or sets the <see cref="EditForm.OnValidSubmit"/> event callback.
    /// </summary>
    [Parameter]
    public EventCallback OnValidSubmit { get; set; }

    /// <summary>
    /// Gets or sets the callback which gets invoked when the cancel button gets clicked.
    /// </summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    [Parameter]
    public ConnectServerDefinition Model { get; set; } = null!;

    private ConnectServerConfigurationViewItem? Configuration { get; set; }

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        base.OnInitialized();

        this.Configuration = new ConnectServerConfigurationViewItem(this.Model);
    }
}