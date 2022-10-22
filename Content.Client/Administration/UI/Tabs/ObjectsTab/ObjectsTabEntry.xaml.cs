﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Administration.UI.Tabs.ObjectsTab;

[GenerateTypedNameReferences]
public sealed partial class ObjectsTabEntry : ContainerButton
{
    public EntityUid AssocEntity;

    public ObjectsTabEntry(string name, EntityUid euid)
    {
        RobustXamlLoader.Load(this);
        AssocEntity = euid;
        EIDLabel.Text = euid.ToString();
        NameLabel.Text = name;
    }
}
