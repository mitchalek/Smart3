namespace Smart3.Protocols
{
    /// <summary>
    /// List of all individual commands exposed by the Smart3 Cash Register communication protocol.
    /// </summary>
    /// <remarks>
    /// When chaining multiple commands each command must be terminated with a <see cref="CommandTypes.Separator"/> character.
    /// Some commands accept additional parameters (see Smart3 Communication Protocol manual).
    /// </remarks>
    internal static class CommandTypes
    {
        public const string Separator = @";";
        public const string Empty = @"0";
        internal static class MiscellaneousCommands
        {
            public const string PrintOnTicket = @"$";
            public const string ShowOnCustomerNumericalDisplay = @"(";
            public const string ShowOnCustomerAlphanumericDisplay = @")";
            public const string ChangeDateTimeDDMMYYhhmm = @"/";
            public const string ChangeHistoryFileTransmissionLevel = @"@";
            public const string PrintOnRemotePrinter = @"[";
            public const string ShowOnOperatorNumericalDisplay = @">";
            public const string ShowOnOperatorAlphanumericDisplay = @"<";
            public const string LockAfterConclusionOfInteractivityInProgress = @"*1";
            public const string LockAfterConclusionOfOperationInProgress = @"*2";
            public const string RemoveLock = @"*3";
            public const string Abort = @"*4";
            public const string DisableTicketCuttingTemporarily = @"*5";
            public const string CarryOutTicketCuttingWhenClosed = @"*6";
            public const string ChangeOperatingModeInactive = @"+0";
            public const string ChangeOperatingModeRegistering = @"+1";
            public const string ChangeOperatingModeReading = @"+2";
            public const string ChangeOperatingModeClosing = @"+3";
            public const string ChangeOperatingModeProgramming = @"+4";
        }
        internal static class ExecutiveCommands
        {
            public const string ImmediateTransmissionOfHelloMessage = @"#A";
            public const string HeaderEnable = @"#a";
            public const string AlarmConditions = @"#B";
            public const string OperatorManagementEnable = @"#b";
            public const string OperatorManagementDisable = @"#c";
            public const string BalancingOfPLUArchive = @"#e";
            public const string AutomaticTransmissionOfHistoryFile = @"#H";
            public const string RealignmentOfHistoryFilePointers = @"#P";
            public const string KeyboardSimulation = @"#S";
        }
        internal static class InteractivityCommands
        {
            public const string B58_TicketDenominationProgramming = @"*a";
            public const string B80_DepartmentProgramming = @"*F";
            public const string B81_PLUProgramming = @"*G";
            public const string B83_OperatorProgramming = @"*H";
            public const string B89_VATTableProgramming = @"*I";
            public const string B71_DirectPLUProgramming = @"*i";
            public const string B82_WaiterProgramming = @"*J";
            public const string B84_SpecialOfferProgramming = @"*L";
            public const string B56_MultipleFunctionProgramming = @"*l";
            public const string B70_CustomerProgramming = @"*M";
            public const string B57_ModifierKeysDiscountProgramming = @"*m";
            public const string B50_PeriodicIconProgramming = @"*N";
            public const string B72_ConnectabilityProgramming = @"*n";
            public const string B51_DepartmentGroupDescriptionProgramming = @"*O";
            public const string B59_CustomerGroupDescriptionProgramming = @"*o";
            public const string B53_CurrencyProgramming = @"*P";
            public const string B73_OperatorAndWaiterEnableProgramming = @"*p";
            public const string B52_CouponProgramming = @"*Q";
            public const string B74_RemotePrinterParameterProgramming = @"*r";
            public const string B75_TicketFlagsProgramming = @"*s";
            public const string B85_TicketHeaderAndFooterProgramming = @"*T";
            public const string B76_GenericProgramming = @"*t";
            public const string B86_PaymentFormProgramming = @"*U";
            public const string B87_CashBoxOperationProgramming = @"*V";
            public const string B77_ChipCardManagementProgramming = @"*v";
            public const string B69_SubtotalPromotionalDiscountProgramming = @"*W";
            public const string B55_CashBoxOperationDescriptionAndFlagsProgramming = @"*X";
            public const string B88_DiscountOrAdditionalChargeModeProgramming = @"*Y";
            public const string B79_KeyboardProgramming = @"*y";
            public const string B54_IconTextProgramming = @"*Z";
        }
        internal static class ReportRequestCommands
        {
            public const string C02_DepartmentReport = @"*A";
            public const string C04_DailyPLUReport = @"*B";
            public const string C34_TicketFormOfPaymentReport = @"*b";
            public const string C05_OperatorReport = @"*C";
            public const string C06_WaiterReport = @"*D";
            public const string C03_VATReport = @"*E";
            public const string C22_FinancialReport = @"*f";
            public const string C32_CouponReport = @"*g";
            public const string C21_HourlyActivityReport = @"*h";
            public const string C04_PeriodicPLUReport = @"*K";
            public const string C35_CustomerGroupReport = @"*q";
            public const string C30_CustomerReport = @"*R";
            public const string C31_CustomerDeferredInvoicesReport = @"*S";
            public const string C33_OpenTableOrBillReport = @"*u";
            public const string C19_CurrencyTotalizerReport = @"*w";
        }
        internal static class LocalProgrammingRequestCommands
        {
            public const string C58_TicketDenominationProgrammingRequest = @"&a";
            public const string C59_CustomerGroupProgrammingRequest = @"&b";
            public const string C60_CustomerChipCardDataProgrammingRequest = @"&c";
            public const string C69_SubtotalPromotionalDiscountProgrammingRequest = @"&f";
            public const string C56_MultipleFunctionProgrammingRequest = @"&H";
            public const string C16_CustomerProgrammingRequest = @"&I";
            public const string C50_PeriodicIconProgrammingRequest = @"&i";
            public const string C15_WaiterProgrammingRequest = @"&K";
            public const string C20_KeyboardProgrammingRequest = @"&k";
            public const string C07_DepartmentProgrammingRequest = @"&L";
            public const string C23_DirectPLUProgrammingRequest = @"&l";
            public const string C08_PLUProgrammingRequest = @"&M";
            public const string C24_ConnectabilityProgrammingRequest = @"&m";
            public const string C09_OperatorProgrammingRequest = @"&N";
            public const string C10_TicketHeaderAndFooterProgrammingRequest = @"&O";
            public const string C25_OperatorAndWaiterEnableProgrammingRequest = @"&o";
            public const string C11_PaymentFormProgrammingRequest = @"&P";
            public const string C51_DepartmentGroupDescriptionProgrammingRequest = @"&p";
            public const string C12_CashBoxOperationProgrammingRequest = @"&Q";
            public const string C26_RemotePrinterParameterProgrammingRequest = @"&q";
            public const string C13_DiscountOrAdditionalChargeModeProgrammingRequest = @"&R";
            public const string C52_CouponProgrammingRequest = @"&r";
            public const string C55_CashBoxOperationDescriptionAndFlagsProgrammingRequest = @"&S";
            public const string C53_CurrencyProgrammingRequest = @"&s";
            public const string C70_ModifierKeysDiscountProgrammingRequest = @"&T";
            public const string C27_TicketFlagsProgrammingRequest = @"&t";
            public const string C28_GenericProgrammingRequest = @"&u";
            public const string C29_ChipCardManagementProgrammingRequest = @"&v";
            public const string C14_VATTableProgrammingRequest = @"&W";
            public const string C54_IconTextProgrammingRequest = @"&Z";
            public const string C78_PLURecordParameterProgrammingRequest = @"&z";
        }
        internal static class ErasureCommands
        {
            public const string PLUArchiveErasure = @"#C";
            public const string CustomerTotalizerZeroSetting = @"#D";
            public const string TicketDataRangeErasure = @"#d";
            public const string PeriodicPaymentFormTotalizerZeroSetting = @"#E";
            public const string CustomerGroupRangeErasure = @"#F";
            public const string PeriodicPLUTotalizerZeroSetting = @"#G";
            public const string PeriodicDepartmentTotalizerZeroSetting = @"#I";
            public const string HistoryFileZeroSetting = @"#i";
            public const string PeriodicVATRateTotalizerZeroSetting = @"#J";
            public const string TimeDataZeroSetting = @"#K";
            public const string PLURangeErasure = @"#L";
            public const string MultipleFunctionRangeErasure = @"#l";
            public const string CustomerArchiveErasure = @"#M";
            public const string CustomerRangeErasure = @"#N";
            public const string CustomerGroupDataRangeErasure = @"#q";
            public const string WaiterTotalizerZeroSetting = @"#X";
            public const string ActiveOperatorZeroSetting = @"#x";
            public const string FiscalClosingPerformance = @"#Z";
        }
    }
}