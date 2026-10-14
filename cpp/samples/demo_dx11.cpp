// BlazeImGui C++ demo: a Win32 + DirectX 11 window with the menu and a page per widget group.
// Build (Developer Command Prompt), with Dear ImGui 1.92+ in IMGUI_DIR (core files + backends/imgui_impl_win32/dx11):
//   cl /std:c++17 /EHsc /O2 /I%IMGUI_DIR% /I..\BlazeImGui demo_dx11.cpp ..\BlazeImGui\*.cpp %IMGUI_DIR%\*.cpp
//      d3d11.lib dxgi.lib user32.lib gdi32.lib
// Flags: --theme light, --page <title>, --screenshot <file.bmp> (renders a few frames, saves the window, exits).
#include <d3d11.h>
#include <windows.h>

#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#include "imgui.h"
#include "imgui_impl_dx11.h"
#include "imgui_impl_win32.h"
#include "blaze_imgui.h"

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND, UINT, WPARAM, LPARAM);

namespace
{
    ID3D11Device* g_device = nullptr;
    ID3D11DeviceContext* g_context = nullptr;
    IDXGISwapChain* g_swapChain = nullptr;
    ID3D11RenderTargetView* g_target = nullptr;

    void CreateTarget()
    {
        ID3D11Texture2D* back = nullptr;
        g_swapChain->GetBuffer(0, IID_PPV_ARGS(&back));
        g_device->CreateRenderTargetView(back, nullptr, &g_target);
        back->Release();
    }

    LRESULT WINAPI WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
    {
        if (ImGui_ImplWin32_WndProcHandler(hWnd, msg, wParam, lParam))
            return true;
        if (msg == WM_SIZE && g_device && wParam != SIZE_MINIMIZED)
        {
            if (g_target) { g_target->Release(); g_target = nullptr; }
            g_swapChain->ResizeBuffers(0, LOWORD(lParam), HIWORD(lParam), DXGI_FORMAT_UNKNOWN, 0);
            CreateTarget();
            return 0;
        }
        if (msg == WM_DESTROY)
        {
            PostQuitMessage(0);
            return 0;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    // The back buffer as a 24-bit BMP.
    void SaveBackBuffer(const char* path)
    {
        ID3D11Texture2D* back = nullptr;
        g_swapChain->GetBuffer(0, IID_PPV_ARGS(&back));
        D3D11_TEXTURE2D_DESC desc{};
        back->GetDesc(&desc);
        desc.Usage = D3D11_USAGE_STAGING;
        desc.BindFlags = 0;
        desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
        desc.MiscFlags = 0;
        ID3D11Texture2D* staging = nullptr;
        g_device->CreateTexture2D(&desc, nullptr, &staging);
        g_context->CopyResource(staging, back);

        D3D11_MAPPED_SUBRESOURCE mapped{};
        g_context->Map(staging, 0, D3D11_MAP_READ, 0, &mapped);
        const int w = static_cast<int>(desc.Width), h = static_cast<int>(desc.Height);
        const int stride = (w * 3 + 3) & ~3;
        std::vector<unsigned char> pixels(static_cast<size_t>(stride) * h);
        for (int y = 0; y < h; ++y)
        {
            const unsigned char* src = static_cast<const unsigned char*>(mapped.pData) + static_cast<size_t>(mapped.RowPitch) * (h - 1 - y);
            for (int x = 0; x < w; ++x)
            {
                pixels[static_cast<size_t>(y) * stride + x * 3 + 0] = src[x * 4 + 2];
                pixels[static_cast<size_t>(y) * stride + x * 3 + 1] = src[x * 4 + 1];
                pixels[static_cast<size_t>(y) * stride + x * 3 + 2] = src[x * 4 + 0];
            }
        }
        g_context->Unmap(staging, 0);
        staging->Release();
        back->Release();

        BITMAPFILEHEADER file{};
        BITMAPINFOHEADER info{};
        info.biSize = sizeof(info);
        info.biWidth = w;
        info.biHeight = h;
        info.biPlanes = 1;
        info.biBitCount = 24;
        file.bfType = 0x4D42;
        file.bfOffBits = sizeof(file) + sizeof(info);
        file.bfSize = file.bfOffBits + static_cast<DWORD>(pixels.size());
        if (FILE* out = std::fopen(path, "wb"))
        {
            std::fwrite(&file, sizeof(file), 1, out);
            std::fwrite(&info, sizeof(info), 1, out);
            std::fwrite(pixels.data(), 1, pixels.size(), out);
            std::fclose(out);
        }
    }
}

int main(int argc, char** argv)
{
    using namespace Blaze;
    const char* screenshot = nullptr;
    const char* startPage = nullptr;
    bool light = false;
    for (int i = 1; i < argc; ++i)
    {
        if (!std::strcmp(argv[i], "--theme") && i + 1 < argc) light = !std::strcmp(argv[++i], "light");
        else if (!std::strcmp(argv[i], "--page") && i + 1 < argc) startPage = argv[++i];
        else if (!std::strcmp(argv[i], "--screenshot") && i + 1 < argc) screenshot = argv[++i];
    }

    WNDCLASSEXW wc = { sizeof(wc), CS_CLASSDC, WndProc, 0, 0, GetModuleHandleW(nullptr), nullptr, nullptr, nullptr, nullptr, L"BlazeDemo", nullptr };
    RegisterClassExW(&wc);
    // A screenshot run renders off-screen so nothing flashes on the desktop.
    const int x = screenshot ? -4000 : 100;
    HWND hwnd = CreateWindowExW(screenshot ? WS_EX_TOOLWINDOW : 0, wc.lpszClassName, L"BlazeImGui C++ demo", WS_OVERLAPPEDWINDOW, x, 100, 1100, 720,
        nullptr, nullptr, wc.hInstance, nullptr);

    DXGI_SWAP_CHAIN_DESC sd{};
    sd.BufferCount = 2;
    sd.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    sd.OutputWindow = hwnd;
    sd.SampleDesc.Count = 1;
    sd.Windowed = TRUE;
    sd.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;
    if (FAILED(D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &sd, &g_swapChain,
        &g_device, nullptr, &g_context)))
        return 1;
    CreateTarget();
    ShowWindow(hwnd, screenshot ? SW_SHOWNOACTIVATE : SW_SHOWDEFAULT);

    ImGui::CreateContext();
    ImGui::GetIO().IniFilename = nullptr;
    ImGui_ImplWin32_Init(hwnd);
    ImGui_ImplDX11_Init(g_device, g_context);

    OverlayOptions options;
    options.Title = "Blaze Demo";
    options.Subtitle = "C++ port";
    options.Theme = light ? ThemeMode::Light : ThemeMode::Dark;
    options.MenuSize = ImVec2(1040, 660);
    Overlay overlay(options);

    bool esp = true, names = true, boxes = false, check = true;
    float distance = 250.0f;
    int mode = 0, segment = 1, key = VK_F6, count = 12;
    std::string name = "Player";
    ImVec4 color = Palette::Hex("#FF6B6B");
    static const char* const modes[] = { "Nearest", "Lowest health", "Crosshair" };
    static const char* const segments[] = { "Low", "Medium", "High" };
    const float series[] = { 12, 18, 15, 22, 28, 24, 31, 29, 35, 33, 40, 38 };

    overlay.menu.AddPage("Controls", ICON_LC_SLIDERS_HORIZONTAL, [&] {
        Ui::Section("General");
        Ui::BeginCard("general", "Visuals", ICON_LC_EYE);
        Ui::SameLineRight(Ui::BadgeSize("Live").x);
        Ui::Badge("Live", Tone::Success);
        Ui::Toggle("Enable visuals", &esp, "Draw players and objects");
        ImGui::BeginDisabled(!esp);
        Ui::Toggle("Names", &names);
        Ui::Toggle("Boxes", &boxes);
        Ui::Slider("Max distance", &distance, 10, 500, "%.0f yd");
        Ui::Combo("Mode", &mode, modes);
        Ui::Row("Quality");
        Ui::Segmented("##quality", &segment, segments);
        Ui::Color("Enemy color", &color);
        ImGui::EndDisabled();
        Ui::EndCard();

        Ui::Section("Input");
        Ui::BeginCard("input");
        Ui::Keybind("Toggle key", &key, "Click, then press a key");
        Ui::TextInput("Name", &name, "Your name");
        Ui::Number("Count", &count, 0.2f, 0, 99);
        Ui::Checkbox("Remember", &check);
        Ui::Button(ICON_LC_SAVE "  Save", ButtonKind::Primary);
        ImGui::SameLine();
        if (Ui::Button("Toast"))
            Notify::Show("Saved", "Settings written to disk", Tone::Success);
        ImGui::SameLine();
        Ui::Button("Delete", ButtonKind::Danger);
        ImGui::SameLine();
        Ui::Button("Cancel", ButtonKind::Ghost);
        Ui::EndCard();
    }, "Toggles, sliders, inputs and buttons", "Main");

    overlay.menu.AddPage("Dashboard", ICON_LC_LAYOUT_DASHBOARD, [&] {
        if (Ui::BeginGrid("stats", 3))
        {
            Ui::NextCell(); Ui::Stat("Players", "24", "+3", Tone::Success, ICON_LC_USERS);
            Ui::NextCell(); Ui::Stat("Frame time", "6.9 ms", "-0.4", Tone::Success, ICON_LC_TIMER);
            Ui::NextCell(); Ui::Stat("Errors", "2", "+1", Tone::Danger, ICON_LC_TRIANGLE_ALERT);
            Ui::EndGrid();
        }
        Ui::BeginCard("chart", "Throughput", ICON_LC_CHART_LINE);
        Ui::LineChart("##series", series, 12, 0.0f, std::nullopt, std::nullopt, "%.0f");
        Ui::EndCard();
        Ui::BeginCard("progress");
        Ui::Progress(0.62f, "Download");
        Ui::Progress(0.25f, "Upload", Tone::Warning);
        Ui::Spinner();
        Ui::EndCard();
    }, nullptr, "Main").Badge = "3";

    overlay.menu.AddPage("Settings", ICON_LC_SETTINGS, [&] {
        Ui::BeginCard("about", "About", ICON_LC_INFO);
        Ui::Hint("BlazeImGui C++ demo. The theme switch is at the bottom of the sidebar.");
        Ui::EndCard();
    }, nullptr, "App");

    overlay.menu.SidebarFooter = [] { Ui::Text(ICON_LC_CIRCLE " Connected", Tone::Success); };
    if (startPage)
        overlay.menu.Select(startPage);
    overlay.Setup();

    int frame = 0;
    bool running = true;
    while (running)
    {
        MSG msg;
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
            if (msg.message == WM_QUIT)
                running = false;
        }
        if (!running)
            break;

        ImGui_ImplDX11_NewFrame();
        ImGui_ImplWin32_NewFrame();
        ImGui::NewFrame();
        if (Input::Pressed(VK_INSERT))
            overlay.ToggleMenu();
        overlay.Render();
        ImGui::Render();

        const ImVec4 clear = Theme::IsDark() ? ImVec4(0.03f, 0.03f, 0.04f, 1) : ImVec4(0.82f, 0.84f, 0.88f, 1);
        const float clearColor[4] = { clear.x, clear.y, clear.z, clear.w };
        g_context->OMSetRenderTargets(1, &g_target, nullptr);
        g_context->ClearRenderTargetView(g_target, clearColor);
        ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());

        if (screenshot && ++frame == 90)
        {
            SaveBackBuffer(screenshot);
            running = false;
        }
        g_swapChain->Present(1, 0);
    }

    ImGui_ImplDX11_Shutdown();
    ImGui_ImplWin32_Shutdown();
    ImGui::DestroyContext();
    g_target->Release();
    g_swapChain->Release();
    g_context->Release();
    g_device->Release();
    DestroyWindow(hwnd);
    UnregisterClassW(wc.lpszClassName, wc.hInstance);
    return 0;
}
