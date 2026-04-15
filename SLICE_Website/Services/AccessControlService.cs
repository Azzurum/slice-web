using System;
using System.Collections.Generic;
using SLICE_Website.Models;

namespace SLICE_Website.Services
{
    public static class AccessControlService
    {
        // System Modules: Defines every secure screen in the application.
        public enum Module
        {
            Dashboard,
            IncomingOrders,     // Receive stock from HQ/Suppliers
            MyInventory,        // Local branch stock management
            RequestStock,       // Request ingredients from HQ
            SalesPOS,           // Point of Sale execution
            ApproveRequests,    // HQ evaluating branch requests
            WasteTracker,       // Spoilage and loss logging
            Reconciliation,     // Physical stock counting
            MenuRegistry,       // Manage products and recipes
            GlobalInventory,    // HQ Master Ingredient list
            UserAdmin,          // Employee account management
            AuditLogs           // System-wide security tracking
        }

        // Role Matrix: Maps specific roles to their allowed modules.
        // NOTE: "Logistics Admin" is handled directly in MainWindow.xaml.cs as a dynamic override.
        private static readonly Dictionary<string, HashSet<Module>> _rolePermissions = new Dictionary<string, HashSet<Module>>(StringComparer.OrdinalIgnoreCase)
        {
            // EXECUTIVE ROLES (Owner / Super-Admin)
            // Focused on big-picture data, configuration, and auditing.
            // Excludes floor operations (IncomingOrders, ApproveRequests) to maintain separation of duties.

            { "Super-Admin", new HashSet<Module> {
                Module.Dashboard,
                Module.MenuRegistry,
                Module.GlobalInventory,
                Module.UserAdmin,
                Module.AuditLogs,
                Module.WasteTracker,
                Module.Reconciliation
            }},

            // OPERATIONAL ROLES (Manager & Clerk)
            // Focused on running their specific branch.
            { "Manager", new HashSet<Module> {
                Module.Dashboard,
                Module.IncomingOrders,
                Module.MyInventory,
                Module.RequestStock,
                Module.SalesPOS,
                Module.ApproveRequests,
                Module.WasteTracker,
                Module.Reconciliation
            }},

            { "Clerk", new HashSet<Module> {
                Module.IncomingOrders,
                Module.MyInventory,
                Module.RequestStock,
                Module.SalesPOS,
                Module.WasteTracker
            }}
        };

        // Verifies if a given role string exists and has permission for the requested module.
        public static bool CanAccess(string role, Module module)
        {
            if (string.IsNullOrWhiteSpace(role)) return false;

            if (_rolePermissions.ContainsKey(role))
            {
                return _rolePermissions[role].Contains(module);
            }

            return false; // Unknown roles default to denied
        }
    }
}