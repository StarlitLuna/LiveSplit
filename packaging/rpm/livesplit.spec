%{!?livesplit_version:%global livesplit_version 0.0.0}
%{!?livesplit_release:%global livesplit_release 1}
%{!?livesplit_rid:%global livesplit_rid linux-x64}
%{!?livesplit_tarball:%global livesplit_tarball livesplit-linux-x64.tar.gz}

Name:           livesplit
Version:        %{livesplit_version}
Release:        %{livesplit_release}%{?dist}
Summary:        Speedrun timer and split tracker

License:        MIT
URL:            https://github.com/LiveSplit/LiveSplit
Source0:        %{livesplit_tarball}
Source1:        org.livesplit.LiveSplit.desktop
Source2:        Icon.svg
Source3:        LICENSE

ExclusiveArch:  x86_64
BuildRequires:  desktop-file-utils
Requires:       hicolor-icon-theme
Requires:       vlc-libs
Requires:       vlc-plugin-ffmpeg

%description
LiveSplit is a timer program for speedrunners that tracks splits, comparisons,
game time, autosplitters, and sharing workflows.

%prep
%setup -q -n livesplit-%{livesplit_rid}

%build

%install
rm -rf %{buildroot}

install -d %{buildroot}%{_libdir}/livesplit
cp -a . %{buildroot}%{_libdir}/livesplit/
chmod 0755 %{buildroot}%{_libdir}/livesplit/LiveSplit

install -d %{buildroot}%{_bindir}
cat > %{buildroot}%{_bindir}/livesplit <<EOF
#!/bin/sh
exec %{_libdir}/livesplit/LiveSplit "\$@"
EOF
chmod 0755 %{buildroot}%{_bindir}/livesplit

install -d %{buildroot}%{_datadir}/applications
sed -e 's/^Exec=.*/Exec=livesplit/' \
    %{SOURCE1} > %{buildroot}%{_datadir}/applications/org.livesplit.LiveSplit.desktop

install -Dm0644 %{SOURCE2} \
    %{buildroot}%{_datadir}/icons/hicolor/scalable/apps/org.livesplit.LiveSplit.svg
install -Dm0644 %{SOURCE3} %{buildroot}%{_licensedir}/%{name}/LICENSE

%check
desktop-file-validate %{buildroot}%{_datadir}/applications/org.livesplit.LiveSplit.desktop

%files
%license %{_licensedir}/%{name}/LICENSE
%{_bindir}/livesplit
%dir %{_libdir}/livesplit
%{_libdir}/livesplit/*
%{_datadir}/applications/org.livesplit.LiveSplit.desktop
%{_datadir}/icons/hicolor/scalable/apps/org.livesplit.LiveSplit.svg

%changelog
* Sat May 02 2026 LiveSplit Linux Port <noreply@livesplit.org> - 0.0.0-1
- Add Fedora RPM packaging for the Linux port.
