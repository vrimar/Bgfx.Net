#include <emscripten/em_js.h>

EM_JS(void*, bgfxNetImportPageDevice, (), {
	const device = Module.preinitializedWebGPUDevice;
	return device ? WebGPU.importJsDevice(device) : 0;
});

// A defined function is what pulls this object out of the archive; an EM_JS import alone is not.
void* bgfx_net_webgpu_page_device(void)
{
	return bgfxNetImportPageDevice();
}
