namespace Smart3.Protocols
{
    /// <summary>
    /// List of all <see cref="MessageData.Type"/> protocoled types exposed by the Smart3 Cash Register communication protocol.
    /// </summary>
    internal static class MessageTypes
    {
        // Hello, status and error indicator.
        public const string A01_HelloMessage = "A01";
        // Subgroup B0x.
        public const string B01_SearchPLU = "B01";
        public const string B02_SearchMiscFunction = "B02";
        public const string B03_SearchCustomer = "B03";
        public const string B04_SearchMultiFunction = "B04";
        // Subgroups B10 – B45.
        public const string B10_InteractivityTicketStart = "B10";
        public const string B11_InteractivityItemDiscountStart = "B11";
        public const string B12_InteractivityWriteOffStart = "B12";
        public const string B13_InteractivityReturnStart = "B13";
        public const string B14_InteractivityDepartmentStart = "B14";
        public const string B15_InteractivitySubtotalStart = "B15";
        public const string B16_InteractivitySubtotalDiscountStart = "B16";
        public const string B17_InteractivityPaymentStart = "B17";
        public const string B18_InteractivityTicketEnd = "B18";
        public const string B19_InteractivityAuxiliaryOperationStart = "B19";
        public const string B20_InteractivityAnnulmentStart = "B20";
        public const string B21_InteractivityRemotePrinterReceiptStart = "B21";
        public const string B22_InteractivityRemotePrinterInvoiceStart = "B22";
        public const string B23_InteractivityKeyTurningStart = "B23";
        public const string B24_InteractivityOperatorStatementStart = "B24";
        public const string B25_InteractivityBalanceRecallStart = "B25";
        public const string B27_InteractivityAuxiliaryOperationEnd = "B27";
        public const string B30_InteractivitySpecialOffer = "B30";
        public const string B40_InteractivityCustomerIndication = "B40";
        public const string B41_InteractivityLimitExceeded = "B41";
        public const string B42_InteractivityPriceListChange = "B42";
        public const string B43_InteractivityTicketSubtotalOffer = "B43";
        public const string B44_InteractivityCouponIssuingStart = "B44";
        public const string B45_InteractivityFiscalClosingEnd = "B45";
        // Subgroups B5x – B8x
        public const string B50_LoadingPeriodicIconProgramming = "B50";
        public const string B51_LoadingDepartmentGroupDescriptionProgramming = "B51";
        public const string B52_LoadingCouponProgramming = "B52";
        public const string B53_LoadingCurrencyProgramming = "B53";
        public const string B54_LoadingIconTextProgramming = "B54";
        public const string B55_LoadingCashBoxOperationDescriptionAndFlagsProgramming = "B55";
        public const string B56_LoadingMultipleFunctionProgramming = "B56";
        public const string B57_LoadingModifierKeysDiscountProgramming = "B57";
        public const string B58_LoadingTicketDenominationProgramming = "B58";
        public const string B59_LoadingCustomerGroupProgramming = "B59";
        public const string B69_LoadingSubtotalPromotionalDiscountProgramming = "B69";
        public const string B70_LoadingCustomerProgramming = "B70";
        public const string B71_LoadingDirectPLUProgramming = "B71";
        public const string B72_LoadingConnectabilityProgramming = "B72";
        public const string B73_LoadingOperatorAndWaiterEnableProgramming = "B73";
        public const string B74_LoadingRemotePrinterProgramming = "B74";
        public const string B75_LoadingTicketFlagProgramming = "B75";
        public const string B76_LoadingGenericProgramming = "B76";
        public const string B77_LoadingChipCardManagementProgramming = "B77";
        public const string B79_LoadingKeyboardProgramming = "B79";
        public const string B80_LoadingDepartmentProgramming = "B80";
        public const string B81_LoadingPLUProgramming = "B81";
        public const string B82_LoadingWaiterProgramming = "B82";
        public const string B83_LoadingOperatorProgramming = "B83";
        public const string B84_LoadingSpecialOfferData = "B84";
        public const string B85_LoadingTicketHeaderAndFooterProgramming = "B85";
        public const string B86_LoadingPaymentFormProgramming = "B86";
        public const string B87_LoadingCashBoxProgramming = "B87";
        public const string B88_LoadingDiscountOrAdditionalChargeModeProgramming = "B88";
        public const string B89_LoadingVATTableProgramming = "B89";
        // Subgroup B9x
        public const string B90_StatusComputerAndConnection = "B90";
        // Fast PLU loading interactivity (non-documented).
        public const string B99_LoadingFastPLUProgramming = "B99";
        // Incoming data from cash register Cxx
        public const string C00_HistoryFileEmpty = "C00";
        public const string C01_TransmissionActivityRecord = "C01";
        public const string C02_TransmissionDepartmentReport = "C02";
        public const string C03_TransmissionVATReport = "C03";
        public const string C04_TransmissionPLUReport = "C04";
        public const string C05_TransmissionOperatorReport = "C05";
        public const string C06_TransmissionWaiterReport = "C06";
        public const string C07_TransmissionLocalDepartmentProgramming = "C07";
        public const string C08_TransmissionLocalPLUProgramming = "C08";
        public const string C09_TransmissionLocalOperatorProgramming = "C09";
        public const string C10_TransmissionLocalTicketHeaderAndFooterProgramming = "C10";
        public const string C11_TransmissionLocalPaymentFormProgramming = "C11";
        public const string C12_TransmissionLocalCashBoxOperationProgramming = "C12";
        public const string C13_TransmissionLocalDiscountOrAdditionalChargeModeProgramming = "C13";
        public const string C14_TransmissionLocalVATTableProgramming = "C14";
        public const string C15_TransmissionLocalWaiterProgramming = "C15";
        public const string C16_TransmissionLocalCustomerProgramming = "C16";
        public const string C19_TransmissionCurrencyTotalizerReport = "C19";
        public const string C20_TransmissionLocalKeyboardProgramming = "C20";
        public const string C21_TransmissionHourlyActivityReport = "C21";
        public const string C22_TransmissionFinancialReport = "C22";
        public const string C23_TransmissionLocalDirectPLUProgramming = "C23";
        public const string C24_TransmissionLocalConnectabilityProgramming = "C24";
        public const string C25_TransmissionLocalWaiterAndOperatorEnableProgramming = "C25";
        public const string C26_TransmissionLocalRemotePrinterProgramming = "C26";
        public const string C27_TransmissionLocalTicketFlagsProgramming = "C27";
        public const string C28_TransmissionLocalGenericProgramming = "C28";
        public const string C29_TransmissionLocalChipCardManagementProgramming = "C29";
        public const string C30_TransmissionCustomerReport = "C30";
        public const string C31_TransmissionCustomerDeferredInvoices = "C31";
        public const string C32_TransmissionCouponReport = "C32";
        public const string C33_TransmissionOpenTableOrBillReport = "C33";
        public const string C34_TransmissionTicketPaymentFormReport = "C34";
        public const string C35_TransmissionCustomerGroupReport = "C35";
        public const string C50_TransmissionLocalPeriodicIconProgramming = "C50";
        public const string C51_TransmissionLocalDepartmentGroupDescriptionProgramming = "C51";
        public const string C52_TransmissionLocalCouponProgramming = "C52";
        public const string C53_TransmissionLocalCurrencyProgramming = "C53";
        public const string C54_TransmissionLocalIconTextProgramming = "C54";
        public const string C55_TransmissionLocalCashBoxOperationDescriptionAndFlagsProgramming = "C55";
        public const string C56_TransmissionLocalMultipleFunctionProgramming = "C56";
        public const string C58_TransmissionLocalTicketDenominationProgramming = "C58";
        public const string C59_TransmissionLocalCustomerGroupProgramming = "C59";
        public const string C60_TransmissionCustomerChipCardData = "C60";
        public const string C69_TransmissionLocalSubtotalPromotionalDiscountProgramming = "C69";
        public const string C70_TransmissionLocalDiscountOrAdditionalChargeModeProgramming = "C70";
        public const string C78_TransmissionLocalPLUParameterProgramming = "C78";
    }
}