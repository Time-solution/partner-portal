using Volo.Abp.Authorization.Permissions;

namespace Zahy.Identity.Permissions;

public class ZahyPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(ZahyPermissions.GroupName);

        var catalog = group.AddPermission(ZahyPermissions.Catalog.Default);
        catalog.AddChild(ZahyPermissions.Catalog.Read);
        catalog.AddChild(ZahyPermissions.Catalog.Write);
        catalog.AddChild(ZahyPermissions.Catalog.AuthorSelf);
        catalog.AddChild(ZahyPermissions.Catalog.AuthorManaged);

        var orders = group.AddPermission(ZahyPermissions.Orders.Default);
        orders.AddChild(ZahyPermissions.Orders.Read);

        var inventory = group.AddPermission(ZahyPermissions.Inventory.Default);
        inventory.AddChild(ZahyPermissions.Inventory.Write);

        var webhooks = group.AddPermission(ZahyPermissions.Webhooks.Default);
        webhooks.AddChild(ZahyPermissions.Webhooks.Manage);

        var payouts = group.AddPermission(ZahyPermissions.Payouts.Default);
        payouts.AddChild(ZahyPermissions.Payouts.Read);

        var partners = group.AddPermission(ZahyPermissions.Partners.Default);
        partners.AddChild(ZahyPermissions.Partners.Manage);

        var roles = group.AddPermission(ZahyPermissions.Roles.Default);
        roles.AddChild(ZahyPermissions.Roles.Manage);

        var finance = group.AddPermission(ZahyPermissions.Finance.Default);
        finance.AddChild(ZahyPermissions.Finance.KycReview);
        finance.AddChild(ZahyPermissions.Finance.ReadAll);
        finance.AddChild(ZahyPermissions.Finance.WriteManualInvoice);

        var commission = group.AddPermission(ZahyPermissions.Commission.Default);
        commission.AddChild(ZahyPermissions.Commission.Approve);

        var settlement = group.AddPermission(ZahyPermissions.Settlement.Default);
        settlement.AddChild(ZahyPermissions.Settlement.Read);
        settlement.AddChild(ZahyPermissions.Settlement.Reconcile);
        settlement.AddChild(ZahyPermissions.Settlement.Disburse);
        settlement.AddChild(ZahyPermissions.Settlement.BankRegistryManage);

        group.AddPermission(ZahyPermissions.Admin);
    }
}
