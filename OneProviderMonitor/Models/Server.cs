using System.ComponentModel.DataAnnotations;

namespace OneProviderMonitor.Models;

public class Server
{
    [Key]
    public int Id { get; set; }
    public bool IsDedicated { get; set; }
    public string CpuMaker { get; set; }
    public string CpuModel { get; set; }
    public double CpuSpeed { get; set; }
    public int CpuCore { get; set; }
    public int CpuThread { get; set; }
    public string LocationName { get; set; }
    public string LocationCode { get; set; }
    public int RamAmount { get; set; }
    public string RamType { get; set; }
    public int StorageSsdMinAmount { get; set; }
    public int StorageHddMinAmount { get; set; }
    public int StorageSsdMaxAmount { get; set; }
    public int StorageHddMaxAmount { get; set; }
    public string StorageJson { get; set; }
    public int BandwidthLimit { get; set; }
    public int BandwidthSpeed { get; set; }
    public int BandwidthGuaranteedSpeed { get; set; }
    public bool BandwidthGuaranteed { get; set; }
    public decimal UsdPriceNormal { get; set; }
    public decimal UsdPricePromo { get; set; }
    public decimal UsdPriceSetup { get; set; }
    public decimal UsdPriceSetupPromo { get; set; }
    public decimal CadPriceNormal { get; set; }
    public decimal CadPricePromo { get; set; }
    public decimal CadPriceSetup { get; set; }
    public decimal CadPriceSetupPromo { get; set; }
    public decimal EurPriceNormal { get; set; }
    public decimal EurPricePromo { get; set; }
    public decimal EurPriceSetup { get; set; }
    public decimal EurPriceSetupPromo { get; set; }
    public bool IsPromo { get; set; }
    public bool LimitedStock { get; set; }
}