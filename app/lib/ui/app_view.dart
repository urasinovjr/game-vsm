import 'package:flutter/material.dart';

import '../app_controller.dart';

const navy = Color(0xFF07162B);
const panel = Color(0xFF102440);
const blue = Color(0xFF087AFF);
const muted = Color(0xFF91A9C6);

class AppView extends StatelessWidget {
  const AppView({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final page = controller.page;
      if (page == null) return const SplashScreen();
      if (page == AppPage.onboarding) {
        return OnboardingScreen(controller: controller);
      }
      return Scaffold(
        body: SafeArea(
          child: Column(
            children: [
              if (controller.busy) const LinearProgressIndicator(minHeight: 2),
              if (controller.error != null)
                ErrorBanner(
                  message: controller.error!,
                  onRetry: page == AppPage.leaderboard
                      ? controller.refreshLeaderboard
                      : controller.refreshProfile,
                ),
              Expanded(
                child: switch (page) {
                  AppPage.home => HomeScreen(controller: controller),
                  AppPage.day => DayScreen(controller: controller),
                  AppPage.profile => ProfileScreen(controller: controller),
                  AppPage.achievements => AchievementsScreen(
                    controller: controller,
                  ),
                  AppPage.leaderboard => LeaderboardScreen(
                    controller: controller,
                  ),
                  AppPage.onboarding => const SizedBox.shrink(),
                },
              ),
              if (page != AppPage.day) NavigationBar(controller: controller),
            ],
          ),
        ),
      );
    },
  );
}

class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) => const Scaffold(
    body: SafeArea(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Spacer(),
          BrandMark(large: true),
          SizedBox(height: 12),
          Text(
            'Учись. Стань профессионалом.\nВыходи на линию.',
            textAlign: TextAlign.center,
            style: TextStyle(color: muted, fontSize: 14),
          ),
          Spacer(),
          Padding(
            padding: EdgeInsets.all(32),
            child: LinearProgressIndicator(color: blue),
          ),
        ],
      ),
    ),
  );
}

class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.large = false});

  final bool large;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(Icons.train_rounded, color: Colors.white, size: large ? 48 : 30),
      const SizedBox(width: 8),
      Text(
        'Проводник\nВСМ',
        style: TextStyle(
          height: 0.98,
          fontSize: large ? 26 : 18,
          fontWeight: FontWeight.w800,
        ),
      ),
    ],
  );
}

class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key, required this.controller});

  final AppController controller;

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  final name = TextEditingController();
  final code = TextEditingController();
  final depot = TextEditingController(text: 'Москва');
  int avatar = 0;

  @override
  void dispose() {
    name.dispose();
    code.dispose();
    depot.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: SafeArea(
      child: ListenableBuilder(
        listenable: widget.controller,
        builder: (context, _) => ListView(
          padding: const EdgeInsets.fromLTRB(22, 28, 22, 28),
          children: [
            const Center(child: BrandMark()),
            const SizedBox(height: 32),
            const Text(
              'Выберите своего проводника',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 21, fontWeight: FontWeight.w800),
            ),
            const SizedBox(height: 8),
            const Text(
              'Он будет сопровождать вас на протяжении\nпрохождения курса',
              textAlign: TextAlign.center,
              style: TextStyle(color: muted, fontSize: 13),
            ),
            const SizedBox(height: 24),
            AvatarPortrait(index: avatar, height: 215),
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(
                4,
                (index) => Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 5),
                  child: InkWell(
                    onTap: () => setState(() => avatar = index),
                    borderRadius: BorderRadius.circular(11),
                    child: Container(
                      width: 51,
                      height: 51,
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(11),
                        border: Border.all(
                          color: avatar == index
                              ? blue
                              : const Color(0xFF335071),
                          width: 2,
                        ),
                        color: panel,
                      ),
                      child: Icon(
                        index.isEven
                            ? Icons.face_3_rounded
                            : Icons.face_rounded,
                        size: 35,
                        color: const Color(0xFFE1A989),
                      ),
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(height: 22),
            AppTextField(
              label: 'Ваше имя',
              hint: 'Например, Даниил',
              controller: name,
            ),
            const SizedBox(height: 11),
            AppTextField(
              label: 'Табельный номер',
              hint: 'Учебный номер',
              controller: code,
            ),
            const SizedBox(height: 11),
            AppTextField(
              label: 'Депо',
              hint: 'Название депо',
              controller: depot,
            ),
            if (widget.controller.error != null) ...[
              const SizedBox(height: 12),
              Text(
                widget.controller.error!,
                style: const TextStyle(color: Color(0xFFFF8484)),
              ),
            ],
            const SizedBox(height: 22),
            PrimaryButton(
              label: widget.controller.busy ? 'Создаём профиль…' : 'Продолжить',
              onPressed: widget.controller.busy
                  ? null
                  : () {
                      if (name.text.trim().isEmpty ||
                          code.text.trim().isEmpty ||
                          depot.text.trim().isEmpty) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(
                            content: Text('Заполните имя, номер и депо'),
                          ),
                        );
                        return;
                      }
                      widget.controller.register(
                        name: name.text,
                        avatar: avatar,
                        employeeCode: code.text,
                        depot: depot.text,
                      );
                    },
            ),
            const SizedBox(height: 10),
            const Text(
              'Используйте вымышленные данные для учебного профиля.',
              textAlign: TextAlign.center,
              style: TextStyle(color: muted, fontSize: 11),
            ),
          ],
        ),
      ),
    ),
  );
}

class AvatarPortrait extends StatelessWidget {
  const AvatarPortrait({super.key, required this.index, required this.height});

  final int index;
  final double height;

  @override
  Widget build(BuildContext context) => Container(
    height: height,
    clipBehavior: Clip.antiAlias,
    decoration: BoxDecoration(
      borderRadius: BorderRadius.circular(20),
      gradient: const LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        colors: [Color(0xFF183D69), Color(0xFF102440), Color(0xFF07162B)],
      ),
    ),
    child: Stack(
      alignment: Alignment.center,
      children: [
        Positioned.fill(child: CustomPaint(painter: StationLinesPainter())),
        Align(
          alignment: Alignment.bottomCenter,
          child: Icon(
            index.isEven ? Icons.face_3_rounded : Icons.face_rounded,
            size: height * 0.8,
            color: const Color(0xFFE1AD8F),
          ),
        ),
      ],
    ),
  );
}

class StationLinesPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = const Color(0x55A4C9F2)
      ..strokeWidth = 1;
    for (var i = 0; i < 8; i++) {
      final x = size.width * i / 7;
      canvas.drawLine(
        Offset(x, 0),
        Offset(x * 0.7 + size.width * 0.15, size.height),
        paint,
      );
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class AppTextField extends StatelessWidget {
  const AppTextField({
    super.key,
    required this.label,
    required this.hint,
    required this.controller,
  });

  final String label;
  final String hint;
  final TextEditingController controller;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(label, style: const TextStyle(color: muted, fontSize: 12)),
      const SizedBox(height: 5),
      TextField(
        controller: controller,
        style: const TextStyle(fontSize: 14),
        decoration: InputDecoration(
          hintText: hint,
          isDense: true,
          filled: true,
          fillColor: panel,
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(10),
            borderSide: BorderSide.none,
          ),
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 14,
            vertical: 12,
          ),
        ),
      ),
    ],
  );
}

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) {
    final profile = controller.profile;
    final identity = controller.identity;
    return RefreshIndicator(
      onRefresh: controller.refreshProfile,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(18, 15, 18, 20),
        children: [
          Row(
            children: [
              const BrandMark(),
              const Spacer(),
              IconButton(
                onPressed: () => controller.open(AppPage.profile),
                icon: const Icon(Icons.notifications_none_rounded),
                tooltip: 'Уведомления',
              ),
            ],
          ),
          const SizedBox(height: 20),
          Container(
            padding: const EdgeInsets.all(13),
            decoration: BoxDecoration(
              color: panel,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 26,
                  backgroundColor: const Color(0xFF1A466F),
                  child: Icon(
                    (identity?.avatar ?? 0).isEven
                        ? Icons.face_3_rounded
                        : Icons.face_rounded,
                    color: const Color(0xFFE1AD8F),
                    size: 34,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        profile?.name ?? 'Проводник',
                        style: const TextStyle(
                          fontWeight: FontWeight.w800,
                          fontSize: 17,
                        ),
                      ),
                      Text(
                        'Проводник · ${identity?.depot ?? 'Депо'}',
                        style: const TextStyle(color: muted, fontSize: 11),
                      ),
                      const SizedBox(height: 7),
                      Text(
                        'Завершено смен: ${profile?.completed ?? 0}',
                        style: const TextStyle(
                          color: Color(0xFF78B5FF),
                          fontSize: 11,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 27),
          const Text(
            'Программа обучения',
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 19),
          ),
          const SizedBox(height: 14),
          DayCard(
            day: 1,
            title: 'Первый рейс',
            description: 'Подготовка к рейсу и работа с пассажирами',
            icon: Icons.train_rounded,
            locked: false,
            onTap: () => controller.open(AppPage.day),
          ),
          const SizedBox(height: 9),
          for (final item in const [
            ('Документы', Icons.badge_outlined),
            ('Багаж', Icons.luggage_rounded),
            ('Нештатные ситуации', Icons.warning_amber_rounded),
            ('Контрольный рейс', Icons.workspace_premium_rounded),
          ].indexed) ...[
            DayCard(
              day: item.$1 + 2,
              title: item.$2.$1,
              description: 'Скоро',
              icon: item.$2.$2,
              locked: true,
            ),
            const SizedBox(height: 9),
          ],
        ],
      ),
    );
  }
}

class DayCard extends StatelessWidget {
  const DayCard({
    super.key,
    required this.day,
    required this.title,
    required this.description,
    required this.icon,
    required this.locked,
    this.onTap,
  });

  final int day;
  final String title;
  final String description;
  final IconData icon;
  final bool locked;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => Material(
    color: locked ? panel : const Color(0xFF06479A),
    borderRadius: BorderRadius.circular(13),
    child: InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(13),
      child: Container(
        padding: const EdgeInsets.all(13),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(13),
          border: locked ? null : Border.all(color: blue),
        ),
        child: Row(
          children: [
            Container(
              width: 38,
              height: 38,
              decoration: BoxDecoration(
                color: locked ? const Color(0xFF1C4269) : blue,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(icon, size: 22),
            ),
            const SizedBox(width: 11),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'День $day',
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 13,
                    ),
                  ),
                  Text(title, style: const TextStyle(fontSize: 11)),
                  Text(
                    description,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(color: muted, fontSize: 10),
                  ),
                ],
              ),
            ),
            Icon(
              locked ? Icons.lock_outline_rounded : Icons.chevron_right_rounded,
              color: locked ? muted : Colors.white,
              size: 20,
            ),
          ],
        ),
      ),
    ),
  );
}

class DayScreen extends StatelessWidget {
  const DayScreen({super.key, required this.controller});

  final AppController controller;

  @override
  Widget build(BuildContext context) => ListView(
    padding: const EdgeInsets.fromLTRB(20, 8, 20, 26),
    children: [
      Row(
        children: [
          IconButton(
            onPressed: () => controller.open(AppPage.home),
            icon: const Icon(Icons.arrow_back_ios_new_rounded),
            tooltip: 'Назад',
          ),
          const Spacer(),
          const Text('День 1', style: TextStyle(fontWeight: FontWeight.w800)),
          const Spacer(),
          const SizedBox(width: 48),
        ],
      ),
      const SizedBox(height: 11),
      const Text(
        'День 1',
        style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800),
      ),
      const Text('Первый рейс', style: TextStyle(color: muted)),
      const SizedBox(height: 14),
      const TrainBanner(height: 155),
      const SizedBox(height: 15),
      Row(
        children: [
          Expanded(child: _Segment(label: 'Рейс туда', active: true)),
          const SizedBox(width: 8),
          Expanded(child: _Segment(label: 'Рейс обратно', active: false)),
        ],
      ),
      const SizedBox(height: 25),
      const Text(
        'Этапы маршрута',
        style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
      ),
      const SizedBox(height: 15),
      for (final step in const [
        ('Прибытие на вокзал', 'Проверка состояния и готовности'),
        ('Посадка', 'Проверка документов, помощь с багажом'),
        ('В пути', 'Работа с пассажирами'),
        ('Высадка', 'Завершение рейса и учёт багажа'),
        ('Сдача вагона', 'Передача вагона, отчётность'),
      ].indexed)
        _TimelineStep(
          number: step.$1 + 1,
          title: step.$2.$1,
          description: step.$2.$2,
          last: step.$1 == 4,
        ),
      const SizedBox(height: 25),
      const _IntegrationNotice(),
    ],
  );
}

class _Segment extends StatelessWidget {
  const _Segment({required this.label, required this.active});
  final String label;
  final bool active;
  @override
  Widget build(BuildContext context) => Container(
    alignment: Alignment.center,
    padding: const EdgeInsets.symmetric(vertical: 10),
    decoration: BoxDecoration(
      color: active ? blue : panel,
      borderRadius: BorderRadius.circular(9),
    ),
    child: Text(
      label,
      style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
    ),
  );
}

class _TimelineStep extends StatelessWidget {
  const _TimelineStep({
    required this.number,
    required this.title,
    required this.description,
    required this.last,
  });
  final int number;
  final String title;
  final String description;
  final bool last;
  @override
  Widget build(BuildContext context) => IntrinsicHeight(
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 25,
          child: Column(
            children: [
              Container(
                width: 12,
                height: 12,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: number == 1 ? blue : navy,
                  border: Border.all(
                    color: number == 1 ? blue : muted,
                    width: 2,
                  ),
                ),
              ),
              if (!last)
                Expanded(
                  child: Container(width: 1, color: const Color(0xFF385777)),
                ),
            ],
          ),
        ),
        Expanded(
          child: Padding(
            padding: const EdgeInsets.only(bottom: 22),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                Text(
                  description,
                  style: const TextStyle(fontSize: 11, color: muted),
                ),
              ],
            ),
          ),
        ),
      ],
    ),
  );
}

class _IntegrationNotice extends StatelessWidget {
  const _IntegrationNotice();
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(15),
    decoration: BoxDecoration(
      color: panel,
      borderRadius: BorderRadius.circular(12),
    ),
    child: const Row(
      children: [
        Icon(Icons.schedule_rounded, color: Color(0xFF78B5FF)),
        SizedBox(width: 12),
        Expanded(
          child: Text(
            'Практический рейс скоро станет доступен.',
            style: TextStyle(fontSize: 12),
          ),
        ),
      ],
    ),
  );
}

class TrainBanner extends StatelessWidget {
  const TrainBanner({super.key, required this.height});
  final double height;
  @override
  Widget build(BuildContext context) => Container(
    height: height,
    clipBehavior: Clip.antiAlias,
    decoration: BoxDecoration(borderRadius: BorderRadius.circular(13)),
    child: Image.asset(
      'assets/images/train-banner.jpg',
      fit: BoxFit.cover,
      alignment: Alignment.centerLeft,
    ),
  );
}

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key, required this.controller});
  final AppController controller;
  @override
  Widget build(BuildContext context) => RefreshIndicator(
    onRefresh: controller.refreshProfile,
    child: ListView(
      padding: const EdgeInsets.all(20),
      children: [
        const PageTitle('Профиль'),
        const SizedBox(height: 20),
        AvatarPortrait(index: controller.identity?.avatar ?? 0, height: 200),
        const SizedBox(height: 16),
        Text(
          controller.profile?.name ?? 'Проводник',
          style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 22),
        ),
        Text(
          '№ ${controller.identity?.employeeCode ?? '—'}  •  ${controller.identity?.depot ?? '—'}',
          style: const TextStyle(color: muted),
        ),
        const SizedBox(height: 21),
        InfoTile(
          icon: Icons.check_circle_outline,
          title: 'Завершено смен',
          value: '${controller.profile?.completed ?? 0}',
        ),
        const SizedBox(height: 8),
        for (final item
            in (controller.profile?.competencies.entries ??
                <MapEntry<String, int>>[]))
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: InfoTile(
              icon: Icons.stars_rounded,
              title: item.key,
              value: '${item.value}',
            ),
          ),
        const SizedBox(height: 19),
        const Text(
          'Уведомления',
          style: TextStyle(fontSize: 17, fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 8),
        for (final note in controller.profile?.notifications ?? <String>[])
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: InfoTile(icon: Icons.notifications_none, title: note),
          ),
      ],
    ),
  );
}

class AchievementsScreen extends StatelessWidget {
  const AchievementsScreen({super.key, required this.controller});
  final AppController controller;
  @override
  Widget build(BuildContext context) {
    final items = controller.profile?.achievements ?? [];
    return RefreshIndicator(
      onRefresh: controller.refreshProfile,
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          const PageTitle('Достижения'),
          const SizedBox(height: 18),
          if (items.isEmpty)
            const EmptyState(
              icon: Icons.workspace_premium_outlined,
              title: 'Достижения впереди',
              description:
                  'После первой завершённой смены здесь появятся награды.',
            ),
          for (final item in items)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: InfoTile(
                icon: Icons.workspace_premium_rounded,
                title: item,
              ),
            ),
        ],
      ),
    );
  }
}

class LeaderboardScreen extends StatelessWidget {
  const LeaderboardScreen({super.key, required this.controller});
  final AppController controller;
  @override
  Widget build(BuildContext context) => RefreshIndicator(
    onRefresh: controller.refreshLeaderboard,
    child: ListView(
      padding: const EdgeInsets.all(20),
      children: [
        const PageTitle('Рейтинг'),
        const SizedBox(height: 6),
        const Text(
          'Лучший результат завершённой смены',
          style: TextStyle(color: muted, fontSize: 12),
        ),
        const SizedBox(height: 18),
        if (controller.leaderboard.isEmpty && !controller.busy)
          const EmptyState(
            icon: Icons.leaderboard_outlined,
            title: 'Пока нет результатов',
            description: 'Завершённые смены участников появятся здесь.',
          ),
        for (final item in controller.leaderboard.indexed)
          Padding(
            padding: const EdgeInsets.only(bottom: 8),
            child: InfoTile(
              icon: Icons.person_outline_rounded,
              title:
                  '${item.$1 + 1}. ${item.$2.name}${item.$2.self ? '  •  Вы' : ''}',
              value: '${item.$2.score}',
            ),
          ),
      ],
    ),
  );
}

class PageTitle extends StatelessWidget {
  const PageTitle(this.text, {super.key});
  final String text;
  @override
  Widget build(BuildContext context) => Text(
    text,
    style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800),
  );
}

class InfoTile extends StatelessWidget {
  const InfoTile({
    super.key,
    required this.icon,
    required this.title,
    this.value,
  });
  final IconData icon;
  final String title;
  final String? value;
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: panel,
      borderRadius: BorderRadius.circular(12),
    ),
    child: Row(
      children: [
        Icon(icon, color: const Color(0xFF78B5FF)),
        const SizedBox(width: 12),
        Expanded(child: Text(title, style: const TextStyle(fontSize: 13))),
        if (value != null)
          Text(value!, style: const TextStyle(fontWeight: FontWeight.w800)),
      ],
    ),
  );
}

class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    required this.description,
  });
  final IconData icon;
  final String title;
  final String description;
  @override
  Widget build(BuildContext context) => Container(
    width: double.infinity,
    padding: const EdgeInsets.symmetric(horizontal: 25, vertical: 45),
    decoration: BoxDecoration(
      color: panel,
      borderRadius: BorderRadius.circular(15),
    ),
    child: Column(
      children: [
        Icon(icon, size: 45, color: const Color(0xFF78B5FF)),
        const SizedBox(height: 13),
        Text(title, style: const TextStyle(fontWeight: FontWeight.w800)),
        const SizedBox(height: 5),
        Text(
          description,
          textAlign: TextAlign.center,
          style: const TextStyle(color: muted, fontSize: 12),
        ),
      ],
    ),
  );
}

class ErrorBanner extends StatelessWidget {
  const ErrorBanner({super.key, required this.message, required this.onRetry});
  final String message;
  final VoidCallback onRetry;
  @override
  Widget build(BuildContext context) => Material(
    color: const Color(0xFF592D3A),
    child: ListTile(
      dense: true,
      leading: const Icon(Icons.wifi_off_rounded, color: Colors.white),
      title: Text(message, style: const TextStyle(fontSize: 12)),
      trailing: IconButton(
        onPressed: onRetry,
        icon: const Icon(Icons.refresh_rounded),
        tooltip: 'Повторить',
      ),
    ),
  );
}

class PrimaryButton extends StatelessWidget {
  const PrimaryButton({
    super.key,
    required this.label,
    required this.onPressed,
  });
  final String label;
  final VoidCallback? onPressed;
  @override
  Widget build(BuildContext context) => SizedBox(
    width: double.infinity,
    height: 46,
    child: FilledButton(
      onPressed: onPressed,
      style: FilledButton.styleFrom(
        backgroundColor: blue,
        foregroundColor: Colors.white,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(11)),
      ),
      child: Text(label, style: const TextStyle(fontWeight: FontWeight.w800)),
    ),
  );
}

class NavigationBar extends StatelessWidget {
  const NavigationBar({super.key, required this.controller});
  final AppController controller;
  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.only(top: 5),
    decoration: const BoxDecoration(
      color: navy,
      border: Border(top: BorderSide(color: Color(0xFF244260))),
    ),
    child: Row(
      children: [
        _NavItem(
          icon: Icons.home_rounded,
          label: 'Главная',
          selected: controller.page == AppPage.home,
          onTap: () => controller.open(AppPage.home),
        ),
        _NavItem(
          icon: Icons.leaderboard_rounded,
          label: 'Рейтинг',
          selected: controller.page == AppPage.leaderboard,
          onTap: () => controller.open(AppPage.leaderboard),
        ),
        _NavItem(
          icon: Icons.workspace_premium_outlined,
          label: 'Достижения',
          selected: controller.page == AppPage.achievements,
          onTap: () => controller.open(AppPage.achievements),
        ),
        _NavItem(
          icon: Icons.person_outline_rounded,
          label: 'Профиль',
          selected: controller.page == AppPage.profile,
          onTap: () => controller.open(AppPage.profile),
        ),
      ],
    ),
  );
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });
  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;
  @override
  Widget build(BuildContext context) => Expanded(
    child: InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 22, color: selected ? blue : muted),
            const SizedBox(height: 3),
            Text(
              label,
              style: TextStyle(
                fontSize: 9,
                color: selected ? Colors.white : muted,
              ),
            ),
          ],
        ),
      ),
    ),
  );
}
