import {
  Component, Input, OnChanges, OnDestroy,
  AfterViewInit, ViewChild, ElementRef, SimpleChanges
} from '@angular/core';
import {
  Chart, ChartDataset, ChartType, LineController, LineElement,
  PointElement, LinearScale, CategoryScale, Title, Tooltip, Legend, Filler,
  ChartOptions, Plugin
} from 'chart.js';

Chart.register(
  LineController, LineElement, PointElement, LinearScale,
  CategoryScale, Title, Tooltip, Legend, Filler
);

export interface DivvyChartDataset {
  label: string;
  data: (number | null)[];
  color: string;
  fill?: boolean;
}

export interface DivvyReferenceBand {
  /** Y value where the dashed line is drawn */
  y: number;
  label: string;
  color: string;
}

@Component({
  selector: 'app-divvy-chart',
  templateUrl: './vitara-chart.component.html',
  styleUrls: ['./vitara-chart.component.css'],
  standalone: false
})
export class DivvyChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('chartCanvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  @Input() title = '';
  @Input() labels: string[] = [];
  @Input() datasets: DivvyChartDataset[] = [];
  @Input() yAxisLabel = '';
  @Input() referenceBands: DivvyReferenceBand[] = [];

  private chart: Chart | null = null;
  private viewReady = false;

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.buildChart();
  }

  ngOnChanges(_: SimpleChanges): void {
    if (this.viewReady) {
      this.buildChart();
    }
  }

  ngOnDestroy(): void {
    this.destroyChart();
  }

  private destroyChart(): void {
    if (this.chart) {
      this.chart.destroy();
      this.chart = null;
    }
  }

  private buildChart(): void {
    if (!this.canvasRef) return;
    this.destroyChart();

    if (!this.labels?.length || !this.datasets?.length) return;

    const refLinePlugin: Plugin<'line'> = {
      id: 'divvyRefLines',
      afterDraw: (chartInstance) => {
        if (!this.referenceBands?.length) return;
        const { ctx, chartArea, scales } = chartInstance as any;
        if (!chartArea || !scales['y']) return;
        ctx.save();
        this.referenceBands.forEach(band => {
          const y = scales['y'].getPixelForValue(band.y);
          if (y < chartArea.top || y > chartArea.bottom) return;
          ctx.beginPath();
          ctx.setLineDash([6, 4]);
          ctx.lineWidth = 1.5;
          ctx.strokeStyle = band.color;
          ctx.moveTo(chartArea.left, y);
          ctx.lineTo(chartArea.right, y);
          ctx.stroke();
          ctx.setLineDash([]);
          ctx.fillStyle = band.color;
          ctx.font = '10px Segoe UI, sans-serif';
          ctx.fillText(band.label, chartArea.left + 4, y - 3);
        });
        ctx.restore();
      }
    };

    const chartDatasets: ChartDataset<'line'>[] = this.datasets.map(ds => ({
      label: ds.label,
      data: ds.data,
      borderColor: ds.color,
      backgroundColor: ds.fill ? ds.color.replace(')', ', 0.1)').replace('rgb', 'rgba') : 'transparent',
      fill: ds.fill ?? false,
      tension: 0.3,
      pointRadius: 4,
      pointHoverRadius: 6,
      borderWidth: 2
    }));

    const options: ChartOptions<'line'> = {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: 'top',
          labels: { font: { size: 12 }, usePointStyle: true }
        },
        title: { display: false },
        tooltip: {
          callbacks: {
            label: (ctx) => `${ctx.dataset.label}: ${ctx.parsed.y !== null ? ctx.parsed.y : 'N/A'}`
          }
        }
      },
      scales: {
        x: {
          grid: { color: 'rgba(0,0,0,0.05)' },
          ticks: { font: { size: 11 } }
        },
        y: {
          title: {
            display: !!this.yAxisLabel,
            text: this.yAxisLabel,
            font: { size: 11 }
          },
          grid: { color: 'rgba(0,0,0,0.07)' },
          ticks: { font: { size: 11 } }
        }
      },
      interaction: { intersect: false, mode: 'index' }
    };

    this.chart = new Chart(this.canvasRef.nativeElement, {
      type: 'line',
      data: { labels: this.labels, datasets: chartDatasets },
      options,
      plugins: [refLinePlugin]
    });
  }
}
